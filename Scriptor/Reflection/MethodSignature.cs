using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Scriptor.Compile;
using Scriptor.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor.Reflection;

/// <summary>
/// An annotated method signature. This may be for a static/instance function, constructor, field, or property.
/// </summary>
public record MethodSignature : IMethodSignature {
    private static readonly List<Type> hideReturnTypes = [];
    private static readonly Dictionary<MemberInfo, MethodSignature> globals = new();
    /// <inheritdoc/>
    public TypeMember Member { get; init; }
    /// <inheritdoc/>
    public MethodFlags Flags { get; set; }
    /// <inheritdoc/>
    public NamedParam[] Params { get; init; }
    /// <inheritdoc/>
    public TypeDesignation.Dummy SharedType { get; init; }
    /// <summary>
    /// The mapping from generic types in the source member object to type designations in <see cref="SharedType"/>.
    /// </summary>
    public Dictionary<Type, TypeDesignation.Variable> GenericTypeMap { get; private set; }
    /// <inheritdoc cref="IGenericMethodSignature.SharedGenericTypes"/>
    public TypeDesignation.Variable[] SharedGenericTypes { get; init; } = Array.Empty<TypeDesignation.Variable>();

    protected MethodSignature(TypeMember Member, NamedParam[] Params) {
        this.Member = Member;
        this.Params = Params;
        GenericTypeMap = new Dictionary<Type, TypeDesignation.Variable>();
        if (Member.BaseMi is MethodBase { IsGenericMethodDefinition: true } mi) {
            var typeRestrs = mi.GetCustomAttributes<RestrictTypesAttribute>()
                .ToDictionary(a => a.typeIndex, 
                    a => a.possibleTypes.Select(t => new TypeDesignation.Known(t)).ToArray());
            SharedGenericTypes = mi.GetGenericArguments()
                .Select((t, i) => GenericTypeMap[t] = new TypeDesignation.Variable()
                    //TryGetValueOrDefault doesn't work on language server
                    { RestrictedTypes = typeRestrs.TryGetValue(i, out var v) ? v : null })
                .ToArray();
        }
        TypeDesignation DesignationForWrappedType(Type t) {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Func<,>)) {
                var gargs = t.GetGenericArguments();
                if (gargs[0] == typeof(TExArgCtx))
                    return DesignationForWrappedType(gargs[1]);
            } else if (t == typeof(TEx))
                //"void-typed" tex- use a variable
                return new TypeDesignation.Variable();
            else if (t.IsTExType(out var inner))
                //typed tex- use the type
                return TypeDesignation.FromType(inner, GenericTypeMap);
            return TypeDesignation.FromType(t, GenericTypeMap);
        }
        
        SharedType = TypeDesignation.Dummy.Method(DesignationForWrappedType(CheckHiddenReturnType(ReturnType)), 
                Params.Select(p => DesignationForWrappedType(p.Type)).ToArray());
    }

    /// <summary>
    /// Check if the return type `t` should be converted to a base type. If so, return the base type.
    /// </summary>
    public static Type CheckHiddenReturnType(Type t) {
        foreach (var rt in hideReturnTypes)
            if (t.IsSubclassOf(rt))
                return rt;
        return t;
    }

    /// <summary>
    /// Mark that any method returning a subclass of `t` should be treated as returning `t` for type unification.
    /// </summary>
    public static void HideReturnType(Type t) => hideReturnTypes.Add(t);
    
    /// <inheritdoc/>
    public bool IsFallthrough { get; init; } = false;
    /// <inheritdoc/>
    public string TypeName => Member.TypeName;
    /// <inheritdoc/>
    public string Name => Member.BaseMi.Name;
    /// <inheritdoc/>
    public bool IsCtor => Member.BaseMi.Name == ".ctor";
    /// <inheritdoc/>
    public bool IsStatic => Member.Static;
    /// <inheritdoc/>
    public Type? DeclaringType => Member.BaseMi.DeclaringType;

    /// <inheritdoc/>
    public virtual Type ReturnType => Member.ReturnType;

    /// <inheritdoc/>
    public string TypeOnlySignature => Member.TypeOnlySignature();

    /// <inheritdoc/>
    public string AsSignature => AsSignatureWithParamMod((p, _) => p.AsParameter);

    /// <inheritdoc/>
    public string AsSignatureWithRestrictions {
        get {
            var sig = AsSignature;
            var restr = GenericTypeMap.Where(kv => kv.Value.RestrictedTypes != null).ToList();
            if (restr.Count == 0) return sig;
            var restrStrs = restr.Select(kv =>
                $"{kv.Key.RName()}:{string.Join(",", kv.Value.RestrictedTypes!.Select(t => t.SimpRName()))}");
            return $"{sig} where {string.Join("; ", restrStrs)}";
        }
    }

    /// <inheritdoc/>
    public string AsSignatureWithParamMod(Func<NamedParam, int, string> paramMod) =>
        Member.AsSignature(paramMod);

    /// <inheritdoc/>
    public T? GetAttribute<T>() where T : Attribute => Member.GetAttribute<T>();

    /// <summary>
    /// Invoke the method with the provided arguments.
    /// </summary>
    public virtual object? Invoke(params object?[] args) =>
        Member.Invoke(args);
    
    /// <summary>
    /// Return the invocation of this method as an expression node.
    /// </summary>
    public virtual Ex InvokeEx(params Ex[] args) =>
        Member.InvokeEx(args);

    /// <inheritdoc/>
    public string? MakeFileLink(string typName) =>
        Member.BaseMi.DeclaringType!.GetCustomAttribute<ReflectAttribute>(false)?.FileLink(typName);

    /// <inheritdoc/>
    public virtual InvokedMethod Call(string? calledAs) => new(this, calledAs);
    
    /// <summary>
    /// Create a Func or Action representing invoking this method.
    /// </summary>
    public object AsFunc(bool compileAsField = false) {
        if (_asFunc != null) return _asFunc;
        if (SharedGenericTypes.Length > 0)
            throw new Exception("Cannot convert a generic method to a partial function");
        var fTypes = SharedType.Arguments.Select(t => t.Resolve().LeftOrThrow).ToArray();
        var args = new IDelegateArg[Params.Length];
        for (int ii = 0; ii < Params.Length; ++ii)
            args[ii] = new DelegateArg($"$parg{ii}", fTypes[ii]);
        var fnType = ReflectionUtils.MakeFuncType(fTypes);
        var serv = ServiceLocator.Find<ILangCustomizer>();
        Func<TExArgCtx, TEx> body = tac => {
            tac.Ctx.CompileToField = compileAsField;
            return AST.MethodCall.RealizeMethod(null, this, tac, (i, tac) => tac.GetByName(fTypes[i], $"$parg{i}"));
        };
        var result = serv.CompileDelegate(fnType, body, args)!;
        //AOT requires ordering consistency; this may be out-of-order if multiple scripts use the same method
        if (ServiceLocator.Find<ILangCustomizer>().AOTMode is AOTMode.None)
            _asFunc = result;
        return result;
    }
    private object? _asFunc;

    /// <summary>
    /// Returns a <see cref="MethodSignature"/> or <see cref="GenericMethodSignature"/> for this method.
    /// </summary>
    public static MethodSignature Get(MemberInfo mi) => 
        MaybeGet(mi) ?? throw new Exception($"Member {mi} cannot be handled by reflection.");
    
    /// <summary>
    /// Returns a <see cref="MethodSignature"/> or <see cref="GenericMethodSignature"/> for this method.
    /// </summary>
    public static MethodSignature? MaybeGet(MemberInfo mi) {
        if (globals.TryGetValue(mi, out var sig))
            return sig;
        var member = TypeMember.MaybeMake(mi);
        if (member == null)
            return null;
        return Get(member);
    }

    public static MethodSignature Get(TypeMember member) {
        var mi = member.BaseMi;
        var fallthrough = mi.GetCustomAttribute<FallthroughAttribute>() != null;
        if (member is TypeMember.Method { Mi : {IsGenericMethodDefinition : true} } inf)
            return globals[mi] = new GenericMethodSignature(inf, member.Params) { IsFallthrough = fallthrough };
        return globals[mi] = new(member, member.Params) { IsFallthrough = fallthrough };
    }

    /// <inheritdoc/>
    public ScopedConversionKind ImplicitTypeConvKind =>
        GetAttribute<ExpressionBoundaryAttribute>() != null ?
            ScopedConversionKind.BlockScopedExpression :
            ScopedConversionKind.Trivial;

    
    /// <inheritdoc cref="LiftedMethodSignature{T}.Lift"/>
    public virtual LiftedMethodSignature<T> Lift<T>() => LiftedMethodSignature<T>.Lift(this);
}

/// <summary>
/// <see cref="IMethodSignature"/> for generic methods.
/// </summary>
public interface IGenericMethodSignature : IMethodSignature {
    /// <summary>
    /// Make a concrete method out of a generic one using the provided type parameter.
    /// </summary>
    MethodSignature Specialize(params Type[] t);
    
    /// <summary>
    /// Get the type designations for each of the generic types of this method.
    /// Note this should not be used for unification, as it is shared between all invocations.
    /// </summary>
    TypeDesignation.Variable[] SharedGenericTypes { get; }
}

/// <inheritdoc cref="MethodSignature"/>
public record GenericMethodSignature(TypeMember.Method Minf, NamedParam[] Params) : MethodSignature(Minf, Params), IGenericMethodSignature {
    /// <summary>
    /// Number of generic parameters.
    /// </summary>
    public int TypeParams { get; } = Minf.Mi.GetGenericArguments().Length;
    public static readonly Dictionary<(FreezableArray<Type>, MethodSignature), MethodSignature> specializeCache = new();
    
    /// <inheritdoc/>
    public override object Invoke(params object?[] prms) {
        throw new Exception("A generic method signature cannot be invoked");
    }

    /// <inheritdoc/>
    public override Ex InvokeEx(params Ex[] args) {
        throw new Exception("A generic method signature cannot be invoked");
    }

    /// <inheritdoc/>
    public MethodSignature Specialize(params Type[] t) {
        var typDef = new FreezableArray<Type>(t);
        var specialized = specializeCache.TryGetValue((typDef, this), out var m) ?
            m :
            specializeCache[(typDef, this)] = MethodSignature.Get(Minf.Mi.MakeGenericMethod(t));
        return specialized;
    }

    /// <inheritdoc/>
    public override LiftedMethodSignature<T> Lift<T>() => LiftGeneric<T>();
    
    /// <inheritdoc cref="Lift{T}"/>
    public GenericLiftedMethodSignature<T> LiftGeneric<T>() => 
        LiftedMethodSignature<T>.Lift(this) as GenericLiftedMethodSignature<T> ??
        throw new StaticException("Incorrect lifting behavior on generic method signature");
}

/// <summary>
/// A description of a funcified method called in reflection.
/// <br/>A funcified method has a "source" signature (A, B, C)->R, but is internally
/// converted to "funcified" signature (T->A, T->B, T->C)->(T->R);
///  ie. it is lifted over the reader functor. This is because
/// some internal reflection functions are of type <see cref="TExArgCtx"/>->TEx,
///  but it is generally easier to write them as type TEx where possible.
/// </summary>
/// <param name="Original">The source method, with the signature (A, B, C)->R.</param>
/// <param name="FuncedParams">The parameter list [T->A, T->B, T->C]. This is provided as <see cref="MethodSignature.Params"/>.</param>
/// <param name="BaseParams">The parameter list [A, B, C].</param>
public abstract record LiftedMethodSignature(MethodSignature Original, NamedParam[] FuncedParams, NamedParam[] BaseParams) 
    : MethodSignature(Original.Member, FuncedParams) {
    protected static readonly Dictionary<(Type, Type), (Type lmsTR, ConstructorInfo constr)> typeSpecCache = new();
    protected static readonly Type[] consTypes = { typeof(MethodSignature), typeof(NamedParam[]), typeof(NamedParam[]) };

    /// <inheritdoc/>
    public override InvokedMethod Call(string? calledAs) => new LiftedInvokedMethod(this, calledAs);
    
    /// <inheritdoc/>
    public override object? Invoke(params object?[] prms) {
        throw new Exception(
            "This lifted method signature does not have a specified return type and therefore cannot be invoked");
    }

    /// <inheritdoc/>
    public override Ex InvokeEx(params Ex[] args) {
        throw new Exception("Lifted methods cannot be invoked as expressions");
    }

    /// <summary>
    /// Lift a set of parameters over the reader functor T->.
    /// </summary>
    public static NamedParam[] LiftParams<T>(MethodSignature method) => LiftParams(typeof(T), method);
    
    public static NamedParam[] LiftParams(Type t, MethodSignature method) {
        var baseTypes = method.Params;
        var fTypes = new NamedParam[baseTypes.Length];
        for (int ii = 0; ii < baseTypes.Length; ++ii) {
            var bt = baseTypes[ii].Type;
            fTypes[ii] = new(TypeLifter.LiftType(t, bt, out var result) ? result : bt,
                baseTypes[ii].Name);
        }
        return fTypes;
    }
}

/// <inheritdoc cref="LiftedMethodSignature"/>
public abstract record LiftedMethodSignature<T>(MethodSignature Original, NamedParam[] FuncedParams, NamedParam[] BaseParams) :
    LiftedMethodSignature(Original, FuncedParams, BaseParams) {
    private static readonly Dictionary<MemberInfo, LiftedMethodSignature<T>> liftCache = new();
    
    /// <summary>
    /// Lift a method over the reader functor T->.
    /// <br/>If R is known statically, use <see cref="LiftedMethodSignature{T,R}"/>'s Lift instead.
    /// </summary>
    public static LiftedMethodSignature<T> Lift(MethodSignature method) {
        if (liftCache.TryGetValue(method.Member.BaseMi, out var sig))
            return sig;
        if (method is LiftedMethodSignature)
            throw new Exception("Tried to lift a method twice");
        if (method is GenericMethodSignature gm)
            return liftCache[method.Member.BaseMi] = new GenericLiftedMethodSignature<T>(gm, gm.Minf, LiftParams<T>(gm), gm.Params);
        return MakeForReturnType(method.ReturnType, method);
    }

    private static LiftedMethodSignature<T> MakeForReturnType(Type r, MethodSignature method) {
        var t = typeof(T);
        if (!typeSpecCache.TryGetValue((t, r), out var info)) {
            var type = typeof(LiftedMethodSignature<,>).MakeGenericType(t, r);
            var cons = type.GetConstructor(consTypes);
            typeSpecCache[(t, r)] = info = (type, cons);
        }
        return liftCache[method.Member.BaseMi] = info.constr!.Invoke(new object[] { method, LiftParams(t, method), method.Params })
            as LiftedMethodSignature<T> ?? throw new StaticException(
            $"Dynamic instantiation of LiftedMethodSignature<{t.SimpRName()},{r.SimpRName()}> failed");
    }
}

/// <inheritdoc cref="LiftedMethodSignature"/>
public record GenericLiftedMethodSignature<T>(MethodSignature Original, TypeMember.Method Minf, NamedParam[] FuncedParams, NamedParam[] BaseParams) : LiftedMethodSignature<T>(Original, FuncedParams, BaseParams), IGenericMethodSignature  {
    /// <inheritdoc/>
    public override Type ReturnType => TypeLifter.Func2Type(typeof(T), base.ReturnType);

    /// <inheritdoc/>
    public override object Invoke(params object?[] prms)
        => throw new Exception("A generic lifted method cannot be invoked");
    
    /// <inheritdoc/>
    public override Ex InvokeEx(params Ex[] args) {
        throw new Exception("Lifted methods cannot be invoked as expressions");
    }
    
    
    /// <inheritdoc cref="GenericMethodSignature.Specialize"/>
    public LiftedMethodSignature<T> Specialize(Type[] t) {
        var typDef = new FreezableArray<Type>(t);
        LiftedMethodSignature<T> method;
        if (GenericMethodSignature.specializeCache.TryGetValue((typDef, this), out var m))
            method = m as LiftedMethodSignature<T> ??
                   throw new StaticException("Cached specialization of lifted generic method failed");
        else {
            GenericMethodSignature.specializeCache[(typDef, this)] = method = 
                MethodSignature.Get(Minf.Mi.MakeGenericMethod(t)).Lift<T>();
        }
        return method;
    }

    MethodSignature IGenericMethodSignature.Specialize(Type[] t) => Specialize(t);
}

//Note that we must eventually specify the R in LiftedMethodSignature in order to ensure that
// InvokeMiFunced creates a correctly-typed Func<T,R>.
/// <inheritdoc cref="LiftedMethodSignature"/>
public record LiftedMethodSignature<T, R>(MethodSignature Original, NamedParam[] FuncedParams, NamedParam[] BaseParams) 
    : LiftedMethodSignature<T>(Original, FuncedParams, BaseParams) {
    private static readonly Dictionary<MemberInfo, LiftedMethodSignature<T, R>> liftCache = new();
    /// <inheritdoc/>
    public override Type ReturnType => typeof(Func<T, R>);

    /// <inheritdoc/>
    public override object Invoke(params object?[] prms) {
        return InvokeMiFunced(prms);
    }
    
    /// <inheritdoc/>
    public override Ex InvokeEx(params Ex[] args) {
        throw new Exception("Lifted methods cannot be invoked as expressions");
    }

    public Func<T,R> InvokeMiFunced(object?[] fprms) => 
        //Note: this lambda capture generally prevents using ArrayCache
        bpi => {
            var baseArgs = new object?[BaseParams.Length];
            for (int ii = 0; ii < baseArgs.Length; ++ii)
                //Convert from funced object to base object (eg. TExArgCtx->TEx<float> to TEx<float>)
                baseArgs[ii] = TypeLifter.Defuncify(
                    BaseParams[ii].Type, FuncedParams[ii].Type, fprms[ii]!, bpi!);
            
            return (R)Member.Invoke(baseArgs)!;
        };

    /// <inheritdoc/>
    public override InvokedMethod Call(string? calledAs) => new LiftedInvokedMethod<T,R>(this, calledAs);

    /// <summary>
    /// Lift a method over the reader functor T->.
    /// <br/>If R is not known statically, use <see cref="LiftedMethodSignature{T}"/>'s Lift instead.
    /// </summary>
    public new static LiftedMethodSignature<T, R> Lift(MethodSignature method) {
        if (liftCache.TryGetValue(method.Member.BaseMi, out var sig))
            return sig;
        //funced methods are not fallthrough
        return liftCache[method.Member.BaseMi] = new(method, LiftedMethodSignature.LiftParams<T>(method), method.Params);
    }
}