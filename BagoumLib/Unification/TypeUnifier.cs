using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using JetBrains.Annotations;

namespace BagoumLib.Unification {

/// <summary>
/// A failure in type unification for <see cref="TypeDesignation"/>.
/// </summary>
[PublicAPI]
public record TypeUnifyErr {
    /// <summary>
    /// Two type designations were expected to be equal, but were not.
    /// </summary>
    public record NotEqual(TypeDesignation LReq, TypeDesignation RReq, 
        TypeDesignation LResolved, TypeDesignation RResolved) : TypeUnifyErr;

    /// <summary>
    /// Two type designations were expected to be equal, but they resolved to variables with misaligned restrictions.
    /// </summary>
    public record IntersectionFailure(TypeDesignation LReq, TypeDesignation RReq, 
        TypeDesignation.Variable LRes, TypeDesignation.Variable RRes) : TypeUnifyErr;
    
    /// <summary>
    /// Two type designations were expected to be equal, but one resolved to a variable whose restrictions
    ///  were not satisfied by the other.
    /// </summary>
    public record RestrictionFailure(TypeDesignation LReq, TypeDesignation RReq, 
        TypeDesignation.Variable LRes, TypeDesignation.Known RRes) : TypeUnifyErr;

    /// <summary>
    /// Two type designations were expected to be equal, but were not.
    /// </summary>
    public record NotEqual<T>(TypeDesignation LReq, TypeDesignation RReq, 
        T LResolvedT, T RResolvedT) : NotEqual(LReq, RReq, LResolvedT, RResolvedT) where T : TypeDesignation;

    /// <summary>
    /// Two resolved type constructors are of different arity.
    /// </summary>
    public record ArityNotEqual(TypeDesignation LeftReq, TypeDesignation RightReq, 
        TypeDesignation LResolved, TypeDesignation RResolved) : TypeUnifyErr;

    /// <summary>
    /// A variable type was never bound to a concrete type.
    /// </summary>
    public record UnboundRestr(TypeDesignation.Variable Req, ITypeTree? Tree) : TypeUnifyErr;

    /// <summary>
    /// A recursive binding was found, where `LResolved` occurred in `RResolved`.
    /// </summary>
    public record RecursionBinding(TypeDesignation LReq, TypeDesignation RReq, 
        TypeDesignation.Variable LResolved, TypeDesignation RResolved) : TypeUnifyErr;

    /// <summary>
    /// During the <see cref="ITypeTree.PossibleUnifiers"/> stage, no overload was found that could
    ///  possibly match the parameters.
    /// </summary>
    public record NoPossibleOverload(IMethodTypeTree Tree, IList<List<(TypeDesignation, Unifier)>> ArgSets) : TypeUnifyErr;
    
    /// <summary>
    /// During the <see cref="ITypeTree.ResolveUnifiers"/> stage, no overload could be found that unified
    ///  correctly with the parameters and return type.
    /// </summary>
    public record NoResolvableOverload(ITypeTree Tree, TypeDesignation Required, IReadOnlyList<(TypeDesignation, TypeUnifyErr)> Overloads) : TypeUnifyErr;

    /// <summary>
    /// Two overloads were found that satisfy the required result type.
    /// </summary>
    public record MultipleOverloads(ITypeTree Tree, TypeDesignation Required, TypeDesignation First, TypeDesignation Second) : TypeUnifyErr;

    /// <summary>
    /// Two implicit casts were found that satisfy the required result type.
    /// </summary>
    public record MultipleImplicits(ITypeTree Tree, TypeDesignation Required, TypeDesignation First, TypeDesignation Second) : TypeUnifyErr;

    /// <summary>
    /// <see cref="ITypeTree.PossibleUnifiers(TypeResolver,Unifier)"/> returned more or less than 1 possible top-level type.
    /// </summary>
    public record TooManyPossibleTypes(ITypeTree Tree, List<TypeDesignation> PossibleTypes) : TypeUnifyErr;

}

//array is not a generic type in C#, so we use this to floss it over.
// ReSharper disable once UnusedTypeParameter
internal class _ArrayGenericTypeHelperDoNotUse<A>;

/// <summary>
/// A restriction on an atomic type or type constructor
/// (where an atomic type is a type that is not the result of a type constructor, ie. int but not List{int})
/// that can be used to perform type unification over two type descriptions with
/// <see cref="TypeDesignation.Unify(TypeDesignation, Unifier)"/>.
/// <br/>This is a more general form of <see cref="ReflectionUtils"/>.ConstructedGenericTypeMatch.
/// </summary>
[PublicAPI]
public abstract class TypeDesignation {
    /// <summary>
    /// The arguments provided to this type constructor (if this is a type constructor).
    /// </summary>
    public TypeDesignation[] Arguments { get; }

    /// <summary>
    /// True iff this type and its arguments are all resolved, and can thus be constructed into a concrete type.
    /// </summary>
    public virtual bool IsResolved => Arguments.All(a => a.IsResolved);

    /// <inheritdoc cref="TypeDesignation"/>
    protected TypeDesignation(params TypeDesignation[] arguments) {
        this.Arguments = arguments;
    }

    /// <summary>
    /// Perform type unification on two type descriptions.
    /// </summary>
    /// <returns>(Left/success) type resolutions, (Right/failure) error that prevented unification</returns>
    public Either<Unifier, TypeUnifyErr> Unify(TypeDesignation other, Unifier unifier) =>
        Unify(this, other, unifier);
    
    /// <inheritdoc cref="Unify(TypeDesignation, Unifier)"/>
    public static Either<Unifier, TypeUnifyErr> Unify(TypeDesignation left, TypeDesignation right, Unifier unifier) {
        var ct = -1;
        while (unifier.Count > ct) {
            ct = unifier.Count;
            var step = UnifyStep(left, right, unifier);
            if (step.IsRight)
                return step.Right;
            unifier = step.Left;
        }
        return unifier;
    }
    private static Either<Unifier, TypeUnifyErr> UnifyStep(TypeDesignation left, TypeDesignation right, Unifier unifier) {
        if (left == right)
            return unifier;
        var ld = unifier[left];
        var rd = unifier[right];
        if (ld == rd)
            return unifier;
        if (unifier.IsYetUnbound(ld, out var lv)) {
            return unifier.Bind(left, right, lv, rd);
        } else if (unifier.IsYetUnbound(rd, out var rv)) {
            return unifier.Bind(right, left, rv, ld);
        } else {
            if (OperatorsEqual(left, right, ld, rd) is { } eqerr)
                return eqerr;
            if (ld.Arguments.Length != rd.Arguments.Length)
                return new TypeUnifyErr.ArityNotEqual(left, right, ld, rd);
            for (int ii = 0; ii < ld.Arguments.Length; ++ii) {
                var nxt = UnifyStep(ld.Arguments[ii], rd.Arguments[ii], unifier);
                if (nxt.IsRight)
                    return nxt.Right;
                unifier = nxt.Left;
            }
            return unifier;
        }
    }

    /// <summary>
    /// Check if a variable type designation occurs in this type tree.
    /// </summary>
    public bool Occurs(Variable v) {
        if (this == v)
            return true;
        for (int ii = 0; ii < Arguments.Length; ++ii)
            if (Arguments[ii].Occurs(v))
                return true;
        return false;
    }

    /// <summary>
    /// Test whether the operators (the atomic type or type constructors) of two designations are equivalent.
    /// <br/>Ignores arguments.
    /// </summary>
    public static TypeUnifyErr? OperatorsEqual(TypeDesignation left, TypeDesignation right,
        TypeDesignation leftResolved, TypeDesignation rightResolved) =>
        (leftResolved, rightResolved) switch {
            (Known kl, Known kr) => kl.Typ != kr.Typ ? new TypeUnifyErr.NotEqual<Known>(left, right, kl, kr) : null,
            (Dummy dl, Dummy dr) => dl.Typ != dr.Typ ? new TypeUnifyErr.NotEqual<Dummy>(left, right, dl, dr) : null,
            (Variable vl, Variable vr) => vl != vr ? new TypeUnifyErr.NotEqual<Variable>(left, right, vl, vr) : null,
            ({ } l, { } r) => new TypeUnifyErr.NotEqual(left, right, l, r)
        };

    /// <summary>
    /// Given the resolutions for unbound types, resolve the final type of this type restriction.
    /// <br/>Note that the return type for a method call or other dummy tree is just its return type/last argument, ignoring its parameters.
    /// </summary>
    /// <returns>(Left/success) resolved type, (Right/failure) error that prevented resolution</returns>
    public abstract Either<Type, TypeUnifyErr> Resolve(Unifier unifier);
    
    /// <inheritdoc cref="Resolve(Unifier)"/>
    public Either<Type, TypeUnifyErr> Resolve() => Resolve(Unifier.Empty);

    /// <summary>
    /// Simplify this type using the unifier.
    /// </summary>
    public abstract TypeDesignation Simplify(Unifier unifier);

    /// <summary>
    /// Check if a variable would occur in this.Simplify(u).
    /// </summary>
    public bool OccursInSimplification(Unifier u, Variable v) {
        if (this is Variable var)
            return var == v || (u.TypeVarBindings.TryGetValue(var, out var nxt) && nxt.OccursInSimplification(u, v));
        for (int ii = 0; ii < Arguments.Length; ++ii)
            if (Arguments[ii].OccursInSimplification(u, v))
                return true;
        return false;
    }

    protected TypeDesignation[]? SimplifyArgs(Unifier u, TypeDesignation[] args) {
        int diff = 0;
        for (; diff < args.Length; ++diff) {
            if (args[diff].Simplify(u) != args[diff])
                goto do_simplify;
        }
        return null;
        do_simplify:
        var nargs = new TypeDesignation[args.Length];
        for (int ii = 0; ii < args.Length; ++ii) {
            nargs[ii] = ii < diff ? args[ii] : args[ii].Simplify(u);
        }
        return nargs;
    }

    /// <summary>
    /// Get all the unbound type variables (<see cref="Variable"/>) in this definition.
    /// </summary>
    public virtual IEnumerable<Variable> GetVariables() => Arguments.SelectMany(a => a.GetVariables());

    /// <summary>
    /// Recreate this type designation with new <see cref="Variable"/>s.
    /// </summary>
    public TypeDesignation RecreateVariables() => RecreateVariables(new());

    /// <inheritdoc cref="RecreateVariables"/>
    protected abstract TypeDesignation RecreateVariables(Dictionary<Variable, Variable> rebind);
    
    /// <summary>
    /// Make an array type designation for this type.
    /// </summary>
    public Known MakeArrayType() => new Known(Known.ArrayGenericType, this);

    public static bool operator ==(TypeDesignation? a, TypeDesignation? b) {
        if (a is null)
            return b is null;
        return a.Equals(b);
    }

    public static bool operator !=(TypeDesignation? a, TypeDesignation? b) => !(a == b);

    // --- subclasses ---
    
    
    /// <summary>
    /// A known atomic type or type constructor.
    /// </summary>
    public sealed class Known : TypeDesignation {
        /// <summary>
        /// The type used to represent array generics (which do not exist natively in C#).
        /// </summary>
        public static Type ArrayGenericType => typeof(_ArrayGenericTypeHelperDoNotUse<>);
        /// <summary>
        /// True iff this type is the type constructor for arrays.
        /// </summary>
        public bool IsArrayTypeConstructor => Typ == ArrayGenericType;
        
        /// <summary>
        /// The known type or type constructor, as a Type object.
        /// </summary>
        public Type Typ { get; init; }

        /// <summary>
        /// A known type.
        /// </summary>
        public Known(Type typ, params TypeDesignation[] arguments) : base(arguments) {
            this.Typ = typ;
        }

        /// <summary>
        /// Create the type Func&lt;a,b,c,d,e...&gt;.
        /// </summary>
        public static Known MakeFuncType(params TypeDesignation[] args) => 
            new(ReflectionUtils.GetFuncType(args.Length), args);
        
        /// <summary>
        /// Create the type (a,b,c,d,e...).
        /// </summary>
        public static Known MakeTupleType(params TypeDesignation[] args) => 
            new(ReflectionUtils.GetTupleType(args.Length), args);

        /// <inheritdoc/>
        public override TypeDesignation Simplify(Unifier unifier) {
            var nargs = SimplifyArgs(unifier, Arguments);
            if (nargs == null) return this;
            return new Known(Typ, nargs);
        }

        /// <inheritdoc/>
        protected override TypeDesignation RecreateVariables(Dictionary<Variable, Variable> rebind)
            => new Known(Typ, Arguments.Select(a => a.RecreateVariables(rebind)).ToArray());

        /// <inheritdoc/>
        public override Either<Type, TypeUnifyErr> Resolve(Unifier unifier) {
            if (Arguments.Length == 0) {
                if (Typ.IsGenericTypeDefinition)
                    throw new Exception($"Unexpected type constructor for {Typ.RName()}");
                return Typ;
            }
            if (!Typ.IsGenericTypeDefinition)
                throw new Exception($"Expected type constructor for {Typ.RName()}");
            if (Typ.GetGenericArguments().Length !=Arguments.Length)
                throw new Exception(
                    $"Type constructor {Typ.RName()} required {Typ.GetGenericArguments().Length} args, " +
                    $"received {Arguments.Length}");
            return Arguments
                .SequenceL(c => c.Resolve(unifier))
                .FMapL(ctypes => 
                    Typ == typeof(_ArrayGenericTypeHelperDoNotUse<>) ?
                        ctypes[0].MakeArrayType() :
                        Typ.MakeGenericType(ctypes.ToArray()));
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Known k && Typ == k.Typ && Arguments.AreSame(k.Arguments);

        /// <inheritdoc/>
        public override int GetHashCode() => (Typ, Arguments.ElementWiseHashCode()).GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => IsArrayTypeConstructor ?
            Arguments[0] + "[]" :
            Arguments.Length == 0 ? 
                Typ.RName() :
                Typ.RName() + $"<{string.Join(",", Arguments.Cast<TypeDesignation>())}>";

    }

    /// <summary>
    /// A dummy type for nesting multiple restriction trees, such as 'method'. The effective return type is the last argument.
    /// </summary>
    public sealed class Dummy : TypeDesignation, IMethodDesignation {
        /// <summary>
        /// ="method"
        /// </summary>
        public const string METHOD_KEY = "method";
        Dummy IMethodDesignation.Method => this;
        
        /// <summary>
        /// The string marking the type of aggregated type this is. By default, this is "method".
        /// </summary>
        public string Typ { get; }
        
        /// <summary>
        /// The last of the aggregated types (also considered the effective return type).
        /// </summary>
        public TypeDesignation Last => Arguments[^1];
        /// <summary>
        /// A dummy type for nesting multiple restriction trees, such as 'method'.
        /// </summary>
        public Dummy(string typ, params TypeDesignation[] arguments) : base(arguments) {
            this.Typ = typ;
        }

        /// <summary>
        /// Create a type designation representing a method call.
        /// </summary>
        /// <param name="returnTyp">Method return type</param>
        /// <param name="argTyps">Method parameter types, preceded by the instance type if this is an instance method</param>
        public static Dummy Method(TypeDesignation returnTyp, params TypeDesignation[] argTyps)
            => new(METHOD_KEY, argTyps.Append(returnTyp).ToArray());

        /// <summary>
        /// For a method tree (a,b,c,d,e)->f, convert it to eg. (a,b)->Func&lt;c,d,e,f&gt; (for args=2).
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public Dummy PartialApplyToFunc(int args) {
            if (args >= Arguments.Length)
                throw new Exception(
                    $"Cannot partially apply {args} arguments to a method with {Arguments.Length - 1} parameters");
            return new Dummy(METHOD_KEY, Arguments.Take(args).Append(
                Known.MakeFuncType(Arguments.Skip(args).ToArray())
            ).ToArray());
        }

        /// <inheritdoc/>
        public override TypeDesignation Simplify(Unifier unifier) => SimplifyDummy(unifier);
        
        /// <inheritdoc cref="Simplify"/>
        public Dummy SimplifyDummy(Unifier unifier) {
            var nargs = SimplifyArgs(unifier, Arguments);
            if (nargs == null) return this;
            return new Dummy(Typ, nargs);
        }

        /// <inheritdoc/>
        public override Either<Type, TypeUnifyErr> Resolve(Unifier unifier) =>
            Arguments[^1].Resolve(unifier);

        /// <inheritdoc/>
        protected override TypeDesignation RecreateVariables(Dictionary<Variable, Variable> rebind)
            => RecreateVariablesD(rebind);
        
        /// <inheritdoc cref="RecreateVariables"/>
        public Dummy RecreateVariablesD(Dictionary<Variable, Variable> rebind)
            => new Dummy(Typ, Arguments.Select(a => a.RecreateVariables(rebind)).ToArray());
        
        /// <inheritdoc cref="RecreateVariables"/>
        public Dummy RecreateVariablesD() => RecreateVariablesD(new());
        
        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Dummy k && Typ == k.Typ && Arguments.AreSame(k.Arguments);

        /// <inheritdoc/>
        public override int GetHashCode() => (Typ, Arguments.ElementWiseHashCode()).GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => $"({string.Join(",", Arguments[..^1].Cast<TypeDesignation>())})->{Last}";
    }

    /// <summary>
    /// An unbound type.
    /// <br/>Note that we currently do not support unification over type constructors, so this cannot take arguments.
    /// </summary>
    public sealed class Variable : TypeDesignation {
        /// <summary>
        /// A random identifier to identify unique unbound types. This is for debugging use only.
        /// </summary>
        private string Ident { get; } = RandUtils.R.RandString(4);
        
        /// <summary>
        /// When Variable represents a literal such as `5`, then RestrictedTypes contains the
        ///  set of types that this variable may take (in this case the set of numeric types).
        /// <br/>Note: The reason why this exists in addition to <see cref="ITypeTree"/> overload handling
        ///  is that overloads on atomic literals (such as `5` possibly being any numeric type) can cause an exponential
        ///  growth in the number of possible unifiers. Consider the case where we have a block statement
        ///     { 1; 2; 3; 4; 5; return 6; }
        ///  and each numeric literal can be `int`, `float`, or `double`. There are 3^6 = 729 possible unifiers!
        /// </summary>
        public IReadOnlyCollection<Known>? RestrictedTypes { get; init; }

        /// <inheritdoc/>
        public override bool IsResolved => false;

        /// <inheritdoc/>
        public override TypeDesignation Simplify(Unifier unifier) => unifier[this];

        /// <inheritdoc/>
        public override Either<Type, TypeUnifyErr> Resolve(Unifier unifier) {
            return unifier[this] switch {
                Variable _ => new TypeUnifyErr.UnboundRestr(this, null),
                { } d => d.Resolve(unifier)
            };
        }

        /// <inheritdoc/>
        public override IEnumerable<Variable> GetVariables() => new[] { this };
        
        //use default reference equality/hash

        /// <inheritdoc/>
        public override string ToString() => $"'T{Ident}";

        /// <inheritdoc/>
        protected override TypeDesignation RecreateVariables(Dictionary<Variable, Variable> rebind)
            => rebind.TryGetValue(this, out var nv) ? nv : rebind[this] = 
                new Variable() { RestrictedTypes = RestrictedTypes };
    }

    /// <summary>
    /// Create a type designation from a type object.
    /// </summary>
    public static TypeDesignation FromType(Type t) {
        return FromType(t, null);
    }

    /// <summary>
    /// Create a type designation from a method.
    /// </summary>
    public static Dummy FromMethod(MethodInfo mi) => FromMethod(mi, out _);

    /// <inheritdoc cref="FromMethod(System.Reflection.MethodInfo)"/>
    public static Dummy FromMethod(MethodInfo mi, out Dictionary<Type, Variable> genericMap)
        => FromMethod(mi.ReturnType, mi.GetParameters().Select(p => p.ParameterType), out genericMap);
    
    /// <inheritdoc cref="FromMethod(System.Reflection.MethodInfo)"/>
    public static Dummy FromMethod(Type returnType, IEnumerable<Type> paramTypes, out Dictionary<Type, Variable> genericMap) {
        var map = genericMap = new Dictionary<Type, Variable>();
        return Dummy.Method(FromType(returnType, map), paramTypes.Select(p => FromType(p, map)).ToArray());
    }
    
    /// <inheritdoc cref="FromMethod(System.Reflection.MethodInfo)"/>
    public static Dummy FromMethod(Type returnType, IEnumerable<Type> paramTypes, Dictionary<Type, Variable> genericMap) =>
        Dummy.Method(FromType(returnType, genericMap), paramTypes.Select(p => FromType(p, genericMap)).ToArray());
    
    
    /// <summary>
    /// Create a type designation from a type object.
    /// </summary>
    public static TypeDesignation FromType(Type t, Dictionary<Type, Variable>? genericMap) {
        if (t.IsArray) {
            return new Known(Known.ArrayGenericType, 
                FromType(t.GetElementType()!, genericMap));
        } else if (t.IsGenericType) {
            return new Known(t.GetGenericTypeDefinition(), 
                t.GetGenericArguments().Select(c => FromType(c, genericMap)).ToArray());
        } else if (t.IsGenericParameter) {
            if (genericMap != null && genericMap.TryGetValue(t, out var generic)) 
                return generic;
            return genericMap != null ?
                genericMap[t] = new Variable() :
                new Variable();
        } else
            return new Known(t);
    }
    
}
}