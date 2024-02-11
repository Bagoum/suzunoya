using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Unification;



/// <summary>
/// A wrapper around a dictionary containing type bindings.
/// <br/>The binding dictionary is immutable.
/// </summary>
[PublicAPI]
public readonly struct Unifier {
    /// <summary>
    /// Binding set.
    /// </summary>
    public ImmutableDictionary<TypeDesignation.Variable, TypeDesignation> TypeVarBindings { get; }
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
            if (d is not TypeDesignation.Variable ub_ || !TypeVarBindings.ContainsKey(ub_))
                return d;
            while (d is TypeDesignation.Variable ub && TypeVarBindings.TryGetValue(ub, out var bound))
                d = bound;
            //Cases where eg. (X, Func<int, B>) and (B, float) are both bound,
            // such that u[X] should return Func<int, float> instead of Func<int, B>
            return d.IsResolved ? d : d.Simplify(this);
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
        if (target.OccursInSimplification(this, v))
            return new TypeUnifyErr.RecursionBinding(vsource, tsource, v, target);
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
    IImplicitTypeConverterInstance NextInstance { get; }
    
    /// <summary>
    /// If false, then the source type will not be converted by this converter, even if it
    ///  matches the converter's required form.
    /// </summary>
    bool SourceAllowed(TypeDesignation source) => true;
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
    IImplicitTypeConverter Converter { get; }
    
    /// <summary>
    /// A method definition in one argument defining the conversion.
    /// </summary>
    TypeDesignation.Dummy MethodType { get; }
    
    /// <summary>
    /// Generic type variables in the method definition. Not sorted.
    /// </summary>
    TypeDesignation.Variable[] Generic { get; }

    /// <summary>
    /// Get the type definitions for each of the generic type variables when this conversion is used
    ///  in the context of the provided unifier.
    /// </summary>
    TypeDesignation[] SimplifyVariables(Unifier u) => Generic.Select(g => u[g]).ToArray();

    /// <summary>
    /// Create a <see cref="IRealizedImplicitCast"/> representing the application of this converter.
    /// </summary>
    IRealizedImplicitCast Realize(Unifier u);

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
    IImplicitTypeConverterInstance Converter { get; }
    /// <summary>
    /// The type to which the object was cast.
    /// </summary>
    TypeDesignation ResultType { get; }
    /// <summary>
    /// The realized types mapped to the <see cref="IImplicitTypeConverterInstance.Generic"/> variables of the type conversion.
    /// </summary>
    TypeDesignation[] Variables { get; }

    /// <summary>
    /// Update the type designations (<see cref="ResultType"/> and <see cref="Variables"/>) with more unification information.
    /// </summary>
    IRealizedImplicitCast Simplify(Unifier u);
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
        /// <inheritdoc cref="IImplicitTypeConverterInstance.Converter"/>
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
