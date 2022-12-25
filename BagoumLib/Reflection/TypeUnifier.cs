using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib.Reflection {

/// <summary>
/// A failure in type unification for <see cref="TypeDesignation"/>.
/// </summary>
[PublicAPI]
public record TypeUnifyErr {
    /// <summary>
    /// Two known types were expected to be equal, but were not.
    /// </summary>
    public record KnownNotEqual(TypeDesignation LReq, TypeDesignation RReq, 
        TypeDesignation.Known LResolved, TypeDesignation.Known RResolved) : TypeUnifyErr;
    /// <summary>
    /// Two type designations were expected to be equal, but were not.
    /// </summary>
    public record NotEqual(TypeDesignation LReq, TypeDesignation RReq, 
            TypeDesignation LResolved, TypeDesignation RResolved) : TypeUnifyErr;

    /// <summary>
    /// Two resolved type constructors are of different arity.
    /// </summary>
    public record ArityNotEqual(TypeDesignation LeftReq, TypeDesignation RightReq, 
        TypeDesignation LResolved, TypeDesignation RResolved) : TypeUnifyErr;

    /// <summary>
    /// A variable type was ever bound to a concrete type.
    /// </summary>
    public record UnboundRestr(TypeDesignation.Variable Req) : TypeUnifyErr;

    /// <summary>
    /// A recursive binding was found, where `LResolved` occurred in `RResolved`.
    /// </summary>
    public record RecursionBinding(TypeDesignation LReq, TypeDesignation RReq, 
        TypeDesignation.Variable LResolved, TypeDesignation RResolved) : TypeUnifyErr;

    /// <summary>
    /// No overload could be found that unified correctly with the parameters.
    /// </summary>
    public record NoOverload(IReadOnlyList<(TypeDesignation, TypeUnifyErr?)> Overloads) : TypeUnifyErr;

    /// <summary>
    /// Two overloads were found that satisfy the required result type.
    /// </summary>
    public record MultipleOverloads(TypeDesignation Required, TypeDesignation First, TypeDesignation Second) : TypeUnifyErr;

    /// <summary>
    /// Two implicit casts were found that satisfy the required result type.
    /// </summary>
    public record MultipleImplicits(TypeDesignation Required, TypeDesignation First, TypeDesignation Second) : TypeUnifyErr;

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
/// <see cref="TypeDesignation.Unify(TypeDesignation)"/>.
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

    protected TypeDesignation(params TypeDesignation[] arguments) {
        this.Arguments = arguments;
    }


    /// <summary>
    /// Perform type unification on two type descriptions.
    /// </summary>
    /// <returns>(Left/success) type resolutions, (Right/failure) error that prevented unification</returns>
    public Either<Unifier, TypeUnifyErr> Unify(TypeDesignation other) =>
        Unify(this, other, Unifier.Empty);


    /// <inheritdoc cref="Unify(TypeDesignation)"/>
    public Either<Unifier, TypeUnifyErr> Unify(TypeDesignation other, Unifier unifier) =>
        Unify(this, other, unifier);
    
    /// <inheritdoc cref="Unify(TypeDesignation)"/>
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
            return unifier.Bind(lv, rd);
        } else if (unifier.IsYetUnbound(rd, out var rv)) {
            if (ld.Occurs(rv))
                return new TypeUnifyErr.RecursionBinding(right, left, rv, ld);
            return unifier.Bind(rv, ld);
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
            (Known kl, Known kr) => kl.Typ != kr.Typ ? new TypeUnifyErr.KnownNotEqual(left, right, kl, kr) : null,
            (Dummy dl, Dummy dr) => dl.Typ != dr.Typ ? new TypeUnifyErr.NotEqual(left, right, dl, dr) : null,
            (Variable vl, Variable vr) => vl != vr ? new TypeUnifyErr.NotEqual(left, right, vl, vr) : null,
            ({ } l, { } r) => new TypeUnifyErr.NotEqual(left, right, l, r)
        };

    /// <summary>
    /// Given the resolutions for unbound types, resolve the final type of this type restriction.
    /// <br/>Note that the return type for a method call or other dummy tree is just its return type/last argument, ignoring its parameters.
    /// </summary>
    /// <returns>(Left/success) resolved type, (Right/failure) error that prevented resolution</returns>
    public abstract Either<Type, TypeUnifyErr> Resolve(Unifier unifier);

    /// <summary>
    /// Simplify this type using the unifier.
    /// </summary>
    public abstract TypeDesignation Simplify(Unifier unifier);

    /// <summary>
    /// Get all the unbound type variables (<see cref="Variable"/>) in this definition.
    /// </summary>
    public virtual IEnumerable<TypeDesignation.Variable> GetVariables() => Arguments.SelectMany(a => a.GetVariables());

    /// <inheritdoc/>
    public override string ToString() => Arguments.Length switch {
        0 => ToStringSelf(),
        1 => $"{ToStringSelf()}({Arguments[0]})",
        _ => $"{ToStringSelf()}(\n\t{string.Join("\n", Arguments.Select(a => a.ToString())).Replace("\n", "\n\t")}\n)"
    };

    /// <summary>
    /// Pretty-print this node (not including any arguments).
    /// </summary>
    protected abstract string ToStringSelf();

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

        /// <inheritdoc/>
        protected override string ToStringSelf() => 
            IsArrayTypeConstructor ? "[]" : Typ.RName();
    }

    /// <summary>
    /// A dummy type for nesting multiple restriction trees, such as 'method'. The effective return type is the last argument.
    /// </summary>
    public sealed class Dummy : TypeDesignation {
        public string Typ { get; }
        public TypeDesignation Last => Arguments[^1];
        /// <summary>
        /// A dummy type for nesting multiple restriction trees, such as 'method'.
        /// </summary>
        public Dummy(string typ, params TypeDesignation[] arguments) : base(arguments) {
            this.Typ = typ;
        }

        public static Dummy Method(TypeDesignation returnTyp, params TypeDesignation[] argTyps)
            => new("method", argTyps.Append(returnTyp).ToArray());

        /// <inheritdoc/>
        public override TypeDesignation Simplify(Unifier unifier) => SimplifyDummy(unifier);
        
        /// <inheritdoc cref="Simplify"/>
        public Dummy SimplifyDummy(Unifier unifier) =>
            new Dummy(Typ, Arguments.Select(a => a.Simplify(unifier)).ToArray());

        /// <inheritdoc/>
        public override Either<Type, TypeUnifyErr> Resolve(Unifier unifier) =>
            Arguments[^1].Resolve(unifier);

        /// <inheritdoc/>
        protected override string ToStringSelf() => Typ;

        /// <summary>
        /// Create a shallow copy of this type designation (recreating the arguments array).
        /// </summary>
        public Dummy Copy() => new(Typ, Arguments.ToArray());
    }

    /// <summary>
    /// An unbound type.
    /// <br/>Note that we currently do not support unification over type constructors, so this cannot take arguments.
    /// </summary>
    public sealed class Variable : TypeDesignation {
        /// <summary>
        /// A random identifier to identify unique unbound types. This is for debugging use only.
        /// </summary>
        private string Ident { get; } = RandUtils.R.RandString(6);

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

        /// <inheritdoc/>
        protected override string ToStringSelf() => $"T_{Ident}";
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
        return new Dummy("method",
            paramTypes.Append(returnType)
                .Select(p => TypeDesignation.FromType(p, map))
                .ToArray()
        );
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
    public Unifier Bind(TypeDesignation.Variable v, TypeDesignation target) {
        if (v == target)
            throw new Exception($"Self-binding of {v} to {target}");
        return new(TypeVarBindings.Add(v, target));
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
    
    /// <summary>
    /// A dictionary mapping a atomic type or type definition to a list of rewrite rules
    ///  that can produce it from another type.
    /// </summary>
    private Dictionary<Type, List<IImplicitTypeConverter>> ReverseRewriteRules { get; } = new();

    public TypeResolver(Dictionary<Type, Type[]> implicitConversions) : this(
        (implicitConversions)
            .SelectMany(kvs => kvs.Value.Select(v => 
            new ImplicitTypeConverter(kvs.Key, v) as IImplicitTypeConverter)).ToArray()) { }
    
    public TypeResolver(params IImplicitTypeConverter[] converters) {
        foreach (var c in converters) {
            if (c.MethodType.Arguments.Length != 2)
                throw new Exception("Implicit type converter must have exactly 1 argument");
            var sourceT = (c.MethodType.Arguments[0] as TypeDesignation.Known) ??
                          throw new Exception("Implicit type converter source type root must be known");
            var targetT = (c.MethodType.Arguments[1] as TypeDesignation.Known) ??
                          throw new Exception("Implicit type converter target type root must be known");
            if (!RewriteRules.ContainsKey(sourceT.Typ))
                RewriteRules[sourceT.Typ] = new();
            RewriteRules[sourceT.Typ].Add(c);
            if (!ReverseRewriteRules.ContainsKey(targetT.Typ))
                ReverseRewriteRules[targetT.Typ] = new();
            ReverseRewriteRules[targetT.Typ].Add(c);
        }
    }

    private static readonly List<IImplicitTypeConverter> empty = new();
    /// <summary>
    /// Return the set of type conversions where `source` is converted into another type.
    /// </summary>
    public bool GetImplicitCasts(TypeDesignation source, out List<IImplicitTypeConverter> conversions) {
        conversions = default!;
        return source is TypeDesignation.Known kt && RewriteRules.TryGetValue(kt.Typ, out conversions);
    }

    /// <inheritdoc cref="GetImplicitCasts(BagoumLib.Reflection.TypeDesignation)"/>
    public List<IImplicitTypeConverter> GetImplicitCasts(TypeDesignation.Known source) =>
        RewriteRules.TryGetValue(source.Typ, out var casts) ? casts : empty;

    /// <summary>
    /// Return the set of type conversions where `target` is constructed from another type.
    /// </summary>
    public bool GetImplicitSources(TypeDesignation target, out List<IImplicitTypeConverter> conversions) {
        conversions = default!;
        return target is TypeDesignation.Known kt && ReverseRewriteRules.TryGetValue(kt.Typ, out conversions);
    }

    /// <inheritdoc cref="GetImplicitSources(BagoumLib.Reflection.TypeDesignation)"/>
    public List<IImplicitTypeConverter> GetImplicitSources(TypeDesignation.Known target) =>
        ReverseRewriteRules.TryGetValue(target.Typ, out var casts) ? casts : empty;
}

/// <summary>
/// Interface for instructions on constructing an implicit type conversions in <see cref="TypeResolver"/>.
/// </summary>
[PublicAPI]
public interface IImplicitTypeConverter {
    /// <summary>
    /// A method definition in one argument defining the conversion.
    /// </summary>
    public TypeDesignation.Dummy MethodType { get; }
    
    /// <summary>
    /// Generic type variables in the method definition. 
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
}

/// <summary>
/// Interface for information about a succesful implicit type conversion in <see cref="TypeResolver"/>.
/// </summary>
[PublicAPI]
public interface IRealizedImplicitCast {
    /// <summary>
    /// The converter that made the conversion.
    /// </summary>
    public IImplicitTypeConverter Converter { get; }
    /// <summary>
    /// The type to which the object was cast.
    /// </summary>
    public TypeDesignation ResultType { get; }
    /// <summary>
    /// The generic variables of the type-conversion pattern match.
    /// </summary>
    public TypeDesignation[] Variables { get; }

    /// <summary>
    /// Update the type designations with more unification information.
    /// </summary>
    public IRealizedImplicitCast Simplify(Unifier u);
}

/// <summary>
/// Basic implementation for <see cref="IImplicitTypeConverter"/>.
/// </summary>
public record ImplicitTypeConverter(TypeDesignation.Dummy MethodType) : IImplicitTypeConverter {
    /// <inheritdoc/>
    public TypeDesignation.Variable[] Generic { get; } = MethodType.GetVariables().Distinct().ToArray();

    public ImplicitTypeConverter(Type from, Type to) : this(TypeDesignation.FromMethod(to, new[] { from }, out _)) { }
    
    /// <inheritdoc/>
    public IRealizedImplicitCast Realize(Unifier u) => new RealizedImplicitCast(this, u);
}

/// <summary>
/// Basic implementation of <see cref="IRealizedImplicitCast"/>.
/// </summary>
public class RealizedImplicitCast : IRealizedImplicitCast {
    /// <inheritdoc/>
    public IImplicitTypeConverter Converter { get; }
    /// <inheritdoc/>
    public TypeDesignation ResultType { get; private set; }
    /// <inheritdoc/>
    public TypeDesignation[] Variables { get; private set; }

    public RealizedImplicitCast(IImplicitTypeConverter converter, Unifier unifier) {
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