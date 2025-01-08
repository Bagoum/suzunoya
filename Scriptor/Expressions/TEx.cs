using System;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor.Expressions;


/// <summary>
/// Base class for <see cref="TEx{T}"/> used for type constraints.
/// </summary>
public class TEx {
    /// <summary>
    /// Injected handler that coverts a data type (eg. typeof(float)) and an expression into a specialized TEx subtype.
    /// </summary>
    public static Func<Type, ParameterExpression, TEx?>? SpecialTypeHandler { get; set; }
    /// <summary>
    /// Expression around which this is wrapped.
    /// </summary>
    public readonly Ex ex;
    /// <summary>
    /// Type of this expression, eg. typeof(float).
    /// </summary>
    /// 
    public readonly Type type;
    
    /// <inheritdoc cref="TEx"/>
    protected TEx(Ex ex) {
        this.ex = ex;
        this.type = ex.Type;
    }

    /// <summary>
    /// Return whether or not a content type matches a TEx type wrapper.
    /// </summary>
    /// <param name="basicType">eg. typeof(float)</param>
    /// <param name="texType">eg. typeof(TEx{float})</param>
    /// <returns></returns>
    public static bool TExTypeMatches(Type basicType, Type texType) {
        if (texType.IsGenericType && texType.GetGenericTypeDefinition() == typeof(TEx<>))
            return texType.GetGenericArguments()[0] == basicType;
        else if (texType.IsSubclassOf(typeof(TEx)))
            return TExTypeMatches(basicType, texType.BaseType!);
        return false;
    }

    /// <summary>
    /// Create a TEx representing a parameter.
    /// </summary>
    /// <typeparam name="T">Type of variable, eg. typeof(float)</typeparam>
    public static TEx MakeParameter<T>(bool isRef, string name) {
        var t = typeof(T);
        var rt = (isRef) ? t.MakeByRefType() : t;
        var ex = Ex.Parameter(rt, name);
        return SpecialTypeHandler?.Invoke(t, ex) ?? new TEx<T>(ex);
    }

    /// <summary>
    /// Create a TEx and a backing ParameterExpression.
    /// </summary>
    protected TEx(ExMode mode, Type t, string? name) {
        if (mode == ExMode.RefParameter) {
            t = t.MakeByRefType();
        }
        ex = name == null ? Ex.Parameter(t) : Ex.Parameter(t, name);
        this.type = ex.Type;
    }
    
    /// <summary>
    /// Create a TEx from an expression.
    /// </summary>
    public static implicit operator TEx(Ex ex) {
        return new(ex);
    }
    
    /// <summary>
    /// Get the expression wrapped within a TEx.
    /// </summary>
    public static implicit operator Ex(TEx me) {
        return me.ex;
    }
    
    /// <summary>
    /// Unsafely convert the wrapped expression into a ParameterExpression.
    /// </summary>
    public static implicit operator ParameterExpression(TEx me) {
        return (ParameterExpression)me.ex;
    }

    /// <summary>
    /// Configuration for copying a repeat expression.
    /// </summary>
    public struct ResolveArg {
        /// <summary>
        /// Source expression.
        /// </summary>
        public readonly Ex ex;
        /// <summary>
        /// Whether or not the expression needs to be compiled.
        /// </summary>
        public readonly bool reqCopy;
        /// <summary>
        /// Name of the expression.
        /// </summary>
        public readonly string? name;
        
        /// <inheritdoc cref="ResolveArg"/>
        public ResolveArg(Ex ex, bool reqCopy, string? name = null) {
            this.ex = ex;
            this.reqCopy = reqCopy;
            this.name = name;
        }
    
        /// <inheritdoc cref="ResolveArg"/>
        public static implicit operator ResolveArg(TEx exx) => new(exx.ex, RequiresCopyOnRepeat(exx.ex));
        /// <inheritdoc cref="ResolveArg"/>
        public static implicit operator ResolveArg(Ex exx) => new(exx, RequiresCopyOnRepeat(exx));
    }
    
    /// <summary>
    /// Create a block expression representing the invocation of `func(args)`, where the arguments
    ///  are stored in block parameters if they need to be copied before use.
    /// </summary>
    public static Ex ResolveCopy(Func<Ex[], Ex> func, params ResolveArg[] args) {
        var newvars = ListCache<ParameterExpression>.Get();
        var setters = ListCache<Ex>.Get();
        var usevars = new Ex[args.Length];
        for (int ii = 0; ii < args.Length; ++ii) {
            if (args[ii].reqCopy) {
                var copy = Ex.Variable(args[ii].ex.Type, args[ii].name);
                usevars[ii] = copy;
                newvars.Add(copy);
                setters.Add(copy.Is(args[ii].ex));
            } else {
                usevars[ii] = args[ii].ex;
            }
        }
        setters.Add(func(usevars));
        var block = Ex.Block(newvars, setters);
        ListCache<ParameterExpression>.Consign(newvars);
        ListCache<Ex>.Consign(setters);
        return block;
    }

    /// <summary>
    /// Create a block expression representing the invocation of `func(arg.fields[0], arg.fields[1]...)`.
    /// If `arg` is a `new Type(nargs)` expression, then remove the `new` call and instead invoke `func(nargs)`,
    ///  where the values in `nargs` are stored in block parameters if `singleUse` is false.
    /// </summary>
    public static Ex ResolveFieldsMaybeDeconstructNew(Func<Ex[], Ex> func, ResolveArg arg, bool singleUse, params string[] fields) {
        if (!arg.reqCopy)
            return func(fields.Select(f => arg.ex.Field(f)).ToArray());
        if (arg.ex is NewExpression newe)
            return singleUse ?
                func(newe.Arguments.ToArray()) :
                ResolveCopy(func, newe.Arguments.Select((x, i) => new ResolveArg(x, RequiresCopyOnRepeat(x), 
                    $"{(x as ParameterExpression)?.Name ?? "anon"}_{fields[i]}")).ToArray());
        if (IsBlockWithLastNew(arg.ex)) {
            var bex = FlattenNestedBlock((BlockExpression)arg.ex);
            return Ex.Block(bex.Variables, bex.Expressions.Take(bex.Expressions.Count - 1).Append(
                ResolveFieldsMaybeDeconstructNew(func, new(bex.Expressions[^1], true), singleUse, fields)));
        }

        var let = Ex.Variable(arg.ex.Type);
        return Ex.Block(new[] { let }, let.Is(arg.ex), func(fields.Select(f => let.Field(f)).ToArray()));
    }

    private static bool IsBlockWithLastNew(Ex ex) {
        while (ex is BlockExpression bex) {
            ex = bex.Expressions[^1];
            if (ex is NewExpression) return true;
        }
        return false;
    }

    private static BlockExpression FlattenNestedBlock(BlockExpression bex) {
        if (bex.Expressions[^1] is not BlockExpression rbex)
            return bex;
        rbex = FlattenNestedBlock(rbex);
        return Ex.Block(bex.Variables.Concat(rbex.Variables),
            bex.Expressions.Take(bex.Expressions.Count - 1).Concat(rbex.Expressions));
    }
    
    /// <inheritdoc cref="ResolveCopy"/>
    public static Ex ResolveF(TEx<float> t1, Func<TEx<float>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0]), t1);
    /// <inheritdoc cref="ResolveCopy"/>
    public static Ex Resolve<T1>(TEx<T1> t1, Func<TEx<T1>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0]), t1);
    /// <inheritdoc cref="ResolveCopy"/>
    public static Ex Resolve<T1,T2>(TEx<T1> t1, TEx<T2> t2, Func<TEx<T1>, TEx<T2>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1]), t1, t2);
    /// <inheritdoc cref="ResolveCopy"/>
    public static Ex Resolve<T1,T2,T3>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2]), t1, t2, t3);
    /// <inheritdoc cref="ResolveCopy"/>
    public static Ex Resolve<T1,T2,T3,T4>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3]), t1, t2, t3, t4);
    /// <inheritdoc cref="ResolveCopy"/>
    public static Ex Resolve<T1,T2,T3,T4,T5>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, TEx<T5> t5, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, TEx<T5>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3], x[4]), t1, t2, t3, t4, t5);
    /// <inheritdoc cref="ResolveCopy"/>
    public static Ex Resolve<T1,T2,T3,T4,T5,T6>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, TEx<T5> t5, 
        TEx<T6> t6, Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, TEx<T5>, TEx<T6>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3], x[4], x[5]), t1, t2, t3, t4, t5, t6);
    /// <inheritdoc cref="ResolveCopy"/>
    public static Ex Resolve<T1,T2,T3,T4,T5,T6,T7>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, TEx<T5> t5, 
        TEx<T6> t6, TEx<T7> t7, Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, TEx<T5>, TEx<T6>, TEx<T7>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3], x[4], x[5], x[6]), t1, t2, t3, t4, t5, t6, t7);
    
    /// <summary>
    /// Return whether or not an expression must be copied into a block parameter if it is used multiple types.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public static bool RequiresCopyOnRepeat(Ex e) => !(
        e.NodeType == ExpressionType.Parameter ||
        e.NodeType == ExpressionType.Constant ||
        e.NodeType == ExpressionType.MemberAccess ||
        (e.NodeType == ExpressionType.Convert && !RequiresCopyOnRepeat((e as UnaryExpression)!.Operand)));
    
}
/// <summary>
/// A typed expression.
/// <br/>This typing is syntactic sugar: any expression, regardless of type, can be cast as eg. TEx{float}.
/// <br/>However, constructing a parameter expression via TEx{T} will type the expression appropriately.
/// By default, creates a ParameterExpression.
/// </summary>
/// <typeparam name="T">Type of expression eg(float).</typeparam>
public class TEx<T> : TEx {

    /// <inheritdoc cref="TEx{T}"/>
    public TEx() : this(ExMode.Parameter, null) {}

    /// <inheritdoc cref="TEx{T}"/>
    public TEx(Ex ex) : base(ex) { }

    /// <inheritdoc cref="TEx{T}"/>
    public TEx(ExMode m, string? name) : base(m, typeof(T), name) {}
    
    /// <inheritdoc cref="TEx{T}"/>
    public static implicit operator TEx<T>(Ex ex) => new(ex);

    /// <inheritdoc cref="TEx{T}"/>
    public static implicit operator TEx<T>(T obj) => Ex.Constant(obj);
}
