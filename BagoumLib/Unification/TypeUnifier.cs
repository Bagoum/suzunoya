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
    public record UnboundRestr(TypeDesignation.Variable Req) : TypeUnifyErr;

    /// <summary>
    /// A recursive binding was found, where `LResolved` occurred in `RResolved`.
    /// </summary>
    public record RecursionBinding(TypeDesignation LReq, TypeDesignation RReq, 
        TypeDesignation.Variable LResolved, TypeDesignation RResolved) : TypeUnifyErr;

    /// <summary>
    /// During the <see cref="ITypeTree.PossibleUnifiers"/> stage, no overload was found that could
    ///  possibly match the parameters.
    /// </summary>
    public record NoPossibleOverload(IMethodTypeTree Tree, List<List<(TypeDesignation, Unifier)>> ArgSets) : TypeUnifyErr;
    
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
    public record TooManyPossibleTypes(List<TypeDesignation> PossibleTypes) : TypeUnifyErr;

}

//array is not a generic type in C#, so we use this to floss it over.
// ReSharper disable once UnusedTypeParameter
internal class _ArrayGenericTypeHelperDoNotUse<A> { }

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
            if (rd.Occurs(lv))
                return new TypeUnifyErr.RecursionBinding(left, right, lv, rd);
            return unifier.Bind(left, right, lv, rd);
        } else if (unifier.IsYetUnbound(rd, out var rv)) {
            if (ld.Occurs(rv))
                return new TypeUnifyErr.RecursionBinding(right, left, rv, ld);
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
        if (Arguments.Length > 0)
            return Arguments.Any(c => c.Occurs(v));
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

        /// <inheritdoc/>
        public override TypeDesignation Simplify(Unifier unifier) =>
            new Known(Typ, Arguments.Select(a => a.Simplify(unifier)).ToArray());

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
                .Select(c => c.Resolve(unifier))
                .SequenceL()
                .FMapL(ctypes => 
                    Typ == typeof(_ArrayGenericTypeHelperDoNotUse<>) ?
                        ctypes[0].MakeArrayType() :
                        Typ.MakeGenericType(ctypes.ToArray()));
        }

        public override bool Equals(object? obj) => obj is Known k && Typ == k.Typ && Arguments.AreSame(k.Arguments);

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

        /// <inheritdoc/>
        public override TypeDesignation Simplify(Unifier unifier) => SimplifyDummy(unifier);
        
        /// <inheritdoc cref="Simplify"/>
        public Dummy SimplifyDummy(Unifier unifier) =>
            new Dummy(Typ, Arguments.Select(a => a.Simplify(unifier)).ToArray());

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
        

        public override bool Equals(object? obj) => obj is Dummy k && Typ == k.Typ && Arguments.AreSame(k.Arguments);

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
                Variable _ => new TypeUnifyErr.UnboundRestr(this),
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
        return FromType(t, new());
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
    
    
    /// <summary>
    /// Create a type designation from a type object.
    /// </summary>
    public static TypeDesignation FromType(Type t, Dictionary<Type, Variable> genericMap) {
        if (t.IsArray) {
            return new Known(Known.ArrayGenericType, 
                FromType(t.GetElementType()!, genericMap));
        } else if (t.IsGenericType) {
            if (!t.IsConstructedGenericType)
                throw new Exception($"Type constructor {t.RName()} used as a known type");
            return new Known(t.GetGenericTypeDefinition(), 
                t.GetGenericArguments().Select(c => FromType(c, genericMap)).ToArray());
        } else if (t.IsGenericParameter) {
            if (genericMap.TryGetValue(t, out var generic)) 
                return generic;
            return genericMap[t] = new Variable();
        } else
            return new Known(t);
    }
    
}

/// <summary>
/// A wrapper around a dictionary containing type bindings.
/// <br/>The binding dictionary is immutable.
/// </summary>
[PublicAPI]
public readonly struct Unifier {
    /// <summary>
    /// Binding set.
    /// </summary>
    private ImmutableDictionary<TypeDesignation.Variable, TypeDesignation> TypeVarBindings { get; }
    /// <summary>
    /// Number of bindings.
    /// </summary>
    public int Count => TypeVarBindings.Count;

    /// <summary>
    /// A unifier with no bindings.
    /// </summary>
    public static readonly Unifier Empty =
        new(ImmutableDictionary<TypeDesignation.Variable, TypeDesignation>.Empty);

    private Unifier(ImmutableDictionary<TypeDesignation.Variable, TypeDesignation> bindings) {
        TypeVarBindings = bindings;
    }

    /// <summary>
    /// Get the ultimate binding of a variable type designation, or the designation itself if it is not variable.
    /// </summary>
    public TypeDesignation this[TypeDesignation d] {
        get {
            while (d is TypeDesignation.Variable ub && TypeVarBindings.TryGetValue(ub, out var bound))
                d = bound;
            return d;
        }
    }

    /// <summary>
    /// Returns true iff `d` is a variable designation that currently has no binding in this unifier.
    /// </summary>
    public bool IsYetUnbound(TypeDesignation d, out TypeDesignation.Variable ub) {
        if (d is TypeDesignation.Variable _ub) {
            ub = _ub;
            return !TypeVarBindings.ContainsKey(ub);
        }
        ub = default!;
        return false;
    }

    /// <summary>
    /// Bind a variable designation to a target.
    /// <br/>Throws if already bound, or the target is the same.
    /// </summary>
    public Either<Unifier, TypeUnifyErr> Bind(TypeDesignation vsource, TypeDesignation tsource, 
            TypeDesignation.Variable v, TypeDesignation target) {
        if (v == target)
            throw new Exception($"Self-binding of {v} to {target}");
        if (v.RestrictedTypes != null) {
            if (target is TypeDesignation.Variable tv) {
                if (tv.RestrictedTypes == null)
                    return new Unifier(TypeVarBindings.Add(tv, v));
                //Intersect type restrictions, create a new variable if neither is strictly better
                var intersection = v.RestrictedTypes.Intersect(tv.RestrictedTypes).ToHashSet();
                if (intersection.Count == tv.RestrictedTypes.Count) {
                    return new Unifier(TypeVarBindings.Add(v, tv));
                } else if (intersection.Count == v.RestrictedTypes.Count) {
                    return new Unifier(TypeVarBindings.Add(tv, v));
                } else if (intersection.Count == 1) {
                    var k = intersection.First();
                    return new Unifier(TypeVarBindings.Add(v, k).Add(tv, k));
                } else if (intersection.Count == 0) {
                    return new TypeUnifyErr.IntersectionFailure(vsource, tsource, v, tv);
                } else {
                    var intV = new TypeDesignation.Variable() { RestrictedTypes = intersection };
                    return new Unifier(TypeVarBindings.Add(v, intV).Add(tv, intV));
                }
            } else if (target is TypeDesignation.Dummy) {
                throw new Exception("Type-restricted variable designation cannot be bound to Dummy");
            } else if (target is TypeDesignation.Known k) {
                if (k.IsResolved && !v.RestrictedTypes.Contains(k))
                    return new TypeUnifyErr.RestrictionFailure(vsource, tsource, v, k);
                if (!k.IsResolved) {
                    Unifier? result = null;
                    foreach (var t in v.RestrictedTypes)
                        if (t.Unify(k, this) is { IsLeft: true } r)
                            //there are two valid types, this is too hard to solve so just generically bind
                            if (result != null)
                                goto success;
                            else
                                result = r.Left;
                    //there is only one valid type, use it
                    if (result.Try(out var res))
                        return new Unifier(res.TypeVarBindings.Add(v, target));
                    return new TypeUnifyErr.RestrictionFailure(vsource, tsource, v, k);
                }
                //otherwise, k.IsResolved and types match, fallthrough to end
            }
        }
        success: ;
        //This includes the case where target has restricted types, in which this binding "passes"
        // the restricted types to v.
        return new Unifier(TypeVarBindings.Add(v, target));
    }
}

/// <summary>
/// Information required to resolve unified types, or to disambiguate overloads, implicit casts, and other
///  supplementary unification features.
/// </summary>
[PublicAPI]
public record TypeResolver {
    /// <summary>
    /// The cache for resolved types.
    /// </summary>
    public Dictionary<TypeDesignation, Type> ResolutionCache { get; } = new();

    /// <summary>
    /// A dictionary mapping a atomic type or type definition to a list of rewrite rules
    ///  that can cast it into another type.
    /// <br/>eg. The rewrite rule for compiling a GCXF from an expression is (Func(TExArgCtx, TEx(_)), GCXF(_)).
    /// <br/>eg. The rewrite rule for getting a float from an int is (int, float).
    /// </summary>
    private Dictionary<Type, List<IImplicitTypeConverter>> RewriteRules { get; } = new();
    //Rewrite rules that apply to all types, eg. (_, Func(TExArgCtx, TEx(_)).
    private List<IImplicitTypeConverter> GlobalRewriteRules { get; } = new();
    
    /// <summary>
    /// A dictionary mapping a atomic type or type definition to a list of rewrite rules
    ///  that can produce it from another type.
    /// </summary>
    private Dictionary<Type, List<IImplicitTypeConverter>> ReverseRewriteRules { get; } = new();

    /// <inheritdoc cref="TypeResolver"/>
    public TypeResolver(Dictionary<Type, Type[]> implicitConversions) : this(
        (implicitConversions)
            .SelectMany(kvs => kvs.Value.Select(v => 
            new ImplicitTypeConverter(kvs.Key, v) as IImplicitTypeConverter)).ToArray()) { }
    
    /// <inheritdoc cref="TypeResolver"/>
    public TypeResolver(params IImplicitTypeConverter[] converters) {
        foreach (var c in converters) {
            var m = c.NextInstance.MethodType;
            if (m.Arguments.Length != 2)
                throw new Exception("Implicit type converter must have exactly 1 argument");
            var targetT = (m.Arguments[1] as TypeDesignation.Known) ??
                          throw new Exception("Implicit type converter target type root must be known");
            if (m.Arguments[0] is TypeDesignation.Variable) {
                GlobalRewriteRules.Add(c);
            } else {
                var sourceT = (m.Arguments[0] as TypeDesignation.Known) ??
                              throw new Exception("Implicit type converter source type root must be known");
                if (!RewriteRules.ContainsKey(sourceT.Typ))
                    RewriteRules[sourceT.Typ] = new();
                RewriteRules[sourceT.Typ].Add(c);
            }
            if (!ReverseRewriteRules.ContainsKey(targetT.Typ))
                ReverseRewriteRules[targetT.Typ] = new();
            ReverseRewriteRules[targetT.Typ].Add(c);
        }
    }

    private static readonly List<IImplicitTypeConverter> empty = new();
    
    /// <summary>
    /// Return the set of type conversions where `source` is converted into another type.
    /// </summary>
    public bool GetImplicitCasts(TypeDesignation source, out IEnumerable<IImplicitTypeConverter> conversions) {
        if (source is TypeDesignation.Known kt && RewriteRules.TryGetValue(kt.Typ, out var _conversions)) {
            conversions = GlobalRewriteRules.Concat(_conversions);
            return true;
        } else {
            conversions = GlobalRewriteRules;
            return GlobalRewriteRules.Count > 0;
        }
    }

    /// <summary>
    /// Return the set of type conversions where `target` is constructed from another type.
    /// </summary>
    public bool GetImplicitSources(TypeDesignation target, out IEnumerable<IImplicitTypeConverter> conversions) {
        if (target is TypeDesignation.Known kt && ReverseRewriteRules.TryGetValue(kt.Typ, out var _conversions)) {
            conversions = _conversions;
            return true;
        } else {
            conversions = default!;
            return false;
        }
    }
    
    /// <summary>
    /// Return the set of type conversions where `target` is constructed from another type.
    /// </summary>
    public bool GetImplicitSourcesList(TypeDesignation target, out List<IImplicitTypeConverter> conversions) {
        if (target is TypeDesignation.Known kt && ReverseRewriteRules.TryGetValue(kt.Typ, out var _conversions)) {
            conversions = _conversions;
            return true;
        } else {
            conversions = default!;
            return false;
        }
    }

}

/// <summary>
/// Interface for instructions on constructing an implicit type conversions in <see cref="TypeResolver"/>.
/// <br/>This allows for generic type conversion when <see cref="IImplicitTypeConverterInstance.Generic"/> is non-empty.
///  For example, if <see cref="IImplicitTypeConverterInstance.MethodType"/> represents the method
///  Foo(Var1) -> Bar(Var1), and Generic contains Var1, then this represents a generic cast from Foo{T} to Bar{T}.
/// </summary>
[PublicAPI]
public interface IImplicitTypeConverter {
    /// <summary>
    /// An instance of this type conversion which has not yet been consumed in a unifier.
    /// </summary>
    public IImplicitTypeConverterInstance NextInstance { get; }
}

/// <summary>
/// An instance of <see cref="IImplicitTypeConverter"/> that does not share generic variables with other instances,
///  in order to prevent cross-pollution of generic conversion variables between multiple areas in a type tree.
/// </summary>
[PublicAPI]
public interface IImplicitTypeConverterInstance {
    /// <summary>
    /// The converter definition.
    /// </summary>
    public IImplicitTypeConverter Converter { get; }
    
    /// <summary>
    /// A method definition in one argument defining the conversion.
    /// </summary>
    public TypeDesignation.Dummy MethodType { get; }
    
    /// <summary>
    /// Generic type variables in the method definition. Not sorted.
    /// </summary>
    public TypeDesignation.Variable[] Generic { get; }

    /// <summary>
    /// Get the type definitions for each of the generic type variables when this conversion is used
    ///  in the context of the provided unifier.
    /// </summary>
    public TypeDesignation[] SimplifyVariables(Unifier u) => Generic.Select(g => u[g]).ToArray();

    /// <summary>
    /// Create a <see cref="IRealizedImplicitCast"/> representing the application of this converter.
    /// </summary>
    public IRealizedImplicitCast Realize(Unifier u);

    /// <summary>
    /// The consumer must call this method when this instance is consumed,
    ///  and the generating <see cref="IImplicitTypeConverter"/> will
    ///  recompute <see cref="IImplicitTypeConverter.NextInstance"/> in order to avoid cross-pollution.
    /// </summary>
    void MarkUsed();
}

/// <summary>
/// Interface for information about a succesful implicit type conversion in <see cref="TypeResolver"/>.
/// <br/>Note that when this is constructed, the result type may not be fully finished, ie. it may still have
///  unbound type variables in it.
/// Call <see cref="Simplify"/> to update this object if further unification is found.
/// </summary>
[PublicAPI]
public interface IRealizedImplicitCast {
    /// <summary>
    /// The converter that made the conversion.
    /// </summary>
    public IImplicitTypeConverterInstance Converter { get; }
    /// <summary>
    /// The type to which the object was cast.
    /// </summary>
    public TypeDesignation ResultType { get; }
    /// <summary>
    /// The realized types mapped to the <see cref="IImplicitTypeConverterInstance.Generic"/> variables of the type conversion.
    /// </summary>
    public TypeDesignation[] Variables { get; }

    /// <summary>
    /// Update the type designations (<see cref="ResultType"/> and <see cref="Variables"/>) with more unification information.
    /// </summary>
    public IRealizedImplicitCast Simplify(Unifier u);
}

/// <summary>
/// Basic implementation for <see cref="IImplicitTypeConverter"/>.
/// </summary>
public record ImplicitTypeConverter : IImplicitTypeConverter {
    //don't use this for unification, it will cross-pollute
    private TypeDesignation.Dummy SharedMethodType { get; }
    public IImplicitTypeConverterInstance NextInstance { get; private set; }

    /// <inheritdoc cref="ImplicitTypeConverter"/>
    public ImplicitTypeConverter(Type from, Type to) : this(TypeDesignation.FromMethod(to, new[] { from }, out _)) { }

    /// <summary>
    /// Basic implementation for <see cref="IImplicitTypeConverter"/>.
    /// </summary>
    public ImplicitTypeConverter(TypeDesignation.Dummy MethodType) {
        this.SharedMethodType = MethodType;
        NextInstance = new Instance(this);
    }

    
    /// <summary>
    /// Basic implementation for <see cref="IImplicitTypeConverterInstance"/>.
    /// </summary>
    public record Instance : IImplicitTypeConverterInstance {
        public ImplicitTypeConverter Converter { get; }
        IImplicitTypeConverter IImplicitTypeConverterInstance.Converter => Converter;
        public TypeDesignation.Dummy MethodType { get; }
        /// <inheritdoc/>
        public TypeDesignation.Variable[] Generic { get; }

        public Instance(ImplicitTypeConverter conv) {
            Converter = conv;
            MethodType = conv.SharedMethodType.RecreateVariablesD();
            Generic = MethodType.GetVariables().Distinct().ToArray();
        }

        /// <inheritdoc/>
        public IRealizedImplicitCast Realize(Unifier u) => new RealizedImplicitCast(this, u);

        /// <inheritdoc/>
        public void MarkUsed() => Converter.NextInstance = new Instance(Converter);
    }
}


/// <summary>
/// Basic implementation of <see cref="IRealizedImplicitCast"/>.
/// </summary>
public class RealizedImplicitCast : IRealizedImplicitCast {
    /// <inheritdoc/>
    public IImplicitTypeConverterInstance Converter { get; }
    /// <inheritdoc/>
    public TypeDesignation ResultType { get; private set; }
    /// <inheritdoc/>
    public TypeDesignation[] Variables { get; private set; }

    /// <inheritdoc cref="RealizedImplicitCast"/>
    public RealizedImplicitCast(IImplicitTypeConverterInstance converter, Unifier unifier) {
        this.Converter = converter;
        //Note that these may not be fully realized at the time of casting
        this.ResultType = Converter.MethodType.Last.Simplify(unifier);
        this.Variables = converter.SimplifyVariables(unifier);
    }

    /// <inheritdoc/>
    public IRealizedImplicitCast Simplify(Unifier u) {
        ResultType = ResultType.Simplify(u);
        Variables = Variables.Select(v => v.Simplify(u)).ToArray();
        return this;
    }
}

}