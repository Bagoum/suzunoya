using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using static BagoumLib.Unification.TypeDesignation;

namespace BagoumLib.Unification {
/// <summary>
/// An AST representing a program that can be used for two-pass type unification with overloading and implicit cast support.
/// </summary>
public interface ITypeTree {
    /// <summary>
    /// First pass, bottom-up: recursively determine all possible types for all operands,
    /// then return the return types of all overloadings that satisfy at least one of the entries
    ///  in the Cartesian product of possible operand sets.
    /// <br/>Implicit casts should be investigated at the *method* level, not the parameter level.
    /// <br/>Note that it is possible to have some outputs that do not actually work, as this may skip some
    ///   parent-child alignment checks for efficiency.
    /// </summary>
    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> PossibleUnifiers(TypeResolver resolver, Unifier unifier);
    
    /// <summary>
    /// Second pass, top-down: After determining one final type for the entire type tree,
    /// find the one overloading that satisfies that type (if there are 0 or 2+, then return an error),
    /// and unify it with any tree children.
    /// <br/>Implicit casts should be realized at the *parameter* level, not the method level.
    /// <br/>This overloading should be saved locally and its return type must be provided as <see cref="SelectedOverloadReturnType"/>.
    /// </summary>
    Either<(TypeDesignation, Unifier), TypeUnifyErr> 
        ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier, bool allowImplicitCast = true);

    /// <summary>
    /// Third pass, readonly: Finalize the types of <see cref="SelectedOverloadReturnType"/>, <see cref="ImplicitCast"/>,
    ///  and any other information that may have been further specified by unification in other parts of the tree.
    /// </summary>
    void FinalizeUnifiers(Unifier unifier);
    
    /// <summary>
    /// The return type of the overloading that was selected for this AST.
    /// <br/>Note that for a method call, this must be the return type of the method.
    /// <br/>This should be overriden by <see cref="ImplicitCast"/> if it is present.
    /// </summary>
    TypeDesignation? SelectedOverloadReturnType { get; }
    
    /// <summary>
    /// The type to which the return type of this AST is implicitly cast.
    /// </summary>
    public IRealizedImplicitCast? ImplicitCast { get; set; }
    
    /// <summary>
    /// True iff the type of this tree and all its components are fully determined.
    /// <br/>This should only be called after <see cref="ResolveUnifiers"/>.
    /// </summary>
    public bool IsFullyResolved => SelectedOverloadReturnType?.IsResolved ?? false;

    /// <summary>
    /// Yields all the unbound variables in the return types selected by <see cref="ResolveUnifiers"/>.
    /// </summary>
    public IEnumerable<Variable> UnresolvedVariables() {
        if (SelectedOverloadReturnType is Variable v)
            yield return v;
    }
}

/// <summary>
/// A proxy for a <see cref="TypeDesignation"/> on a method.
/// </summary>
public interface IMethodDesignation {
    /// <summary>
    /// The type designation for this method.
    /// </summary>
    Dummy Method { get; }
}

/// <summary>
/// Non-generic base interface for <see cref="IMethodTypeTree{T}"/>.
/// </summary>
public interface IMethodTypeTree : ITypeTree {
}

/// <summary>
/// Interface that auto-implements part of <see cref="ITypeTree"/> for overloaded method call ASTs.
/// </summary>
/// <typeparam name="T">Implementation type for <see cref="IMethodDesignation"/>, which specifies the type of each method overload.</typeparam>
public interface IMethodTypeTree<T>: IMethodTypeTree where T: IMethodDesignation {
    /// <summary>
    /// The set of method overloads. All overloads must have the same number of arguments.
    /// <br/>This may be dependent on argument types (such as in the case of member access),
    ///  in which case it can be filled out in <see cref="GenerateOverloads"/>.
    /// </summary>
    IReadOnlyList<T> Overloads { get; }
    
    
    /// <summary>
    /// If this method's overloads are dependent on argument types (such as in the case of member access),
    ///  set <see cref="Overloads"/> based on the possible argument types.
    /// </summary>
    void GenerateOverloads(List<List<(TypeDesignation, Unifier)>> arguments) { }

    /// <summary>
    /// Whether or not the parameter at `index` can receive implicit casts.
    /// </summary>
    bool ImplicitParameterCastEnabled(int index) => true;

    /// <summary>
    /// If this is set to true and multiple overloads can resolve a function in the
    /// <see cref="ITypeTree.ResolveUnifiers"/> stage, then instead of throwing an error,
    /// use the first one that succeeds.
    /// </summary>
    bool PreferFirstOverload => false;
    
    /// <summary>
    /// The subset of <see cref="Overloads"/> that can be realized given an initial parse of the provided arguments (ignoring the return type).
    /// Set in <see cref="ITypeTree.PossibleUnifiers"/>.
    /// </summary>
    List<T>? RealizableOverloads { get; set; }
    
    /// <summary>
    /// The set of arguments to the method, whichever of the overload is selected.
    /// </summary>
    IReadOnlyList<ITypeTree> Arguments { get; }
    
    /// <summary>
    /// The method overload selected by <see cref="ITypeTree.ResolveUnifiers"/>.
    /// </summary>
    (T method, Dummy simplified)? SelectedOverload { get; set; }

    /// <summary>
    /// Called in <see cref="ITypeTree.ResolveUnifiers"/> when the overload is selected,
    ///  and before arguments are unified against the overload.
    /// <br/>This generally requires no implementation, except when overloads or casts carry implicit
    ///  variable declarations.
    /// </summary>
    Either<Unifier, TypeUnifyErr> WillSelectOverload(T method, IImplicitTypeConverterInstance? cast, Unifier u) => u;

    TypeDesignation ITypeTree.SelectedOverloadReturnType =>
        ImplicitCast?.ResultType ??
        (SelectedOverload ?? throw new Exception("Overload not yet finalized")).simplified.Last;

    bool ITypeTree.IsFullyResolved => 
        (SelectedOverloadReturnType?.IsResolved ?? false) && Arguments.All(a => a.IsFullyResolved);


    IEnumerable<Variable> ITypeTree.UnresolvedVariables() {
        if (SelectedOverloadReturnType is Variable v)
            yield return v;
        foreach (var cv in Arguments.SelectMany(a => a.UnresolvedVariables()))
            yield return cv;
    }

    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> ITypeTree.
        PossibleUnifiers(TypeResolver resolver, Unifier unifier) => _PossibleUnifiers(this, resolver, unifier);
    
    /// <inheritdoc cref="ITypeTree.PossibleUnifiers"/>
    public static Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> 
        _PossibleUnifiers(IMethodTypeTree<T> me, TypeResolver resolver, Unifier unifier) {
        //for each method:
        //  for each argument set types in the cartesian product of possible argument types,
        //    including implicit casts from the base argument types,
        //  check if the method can be unified with the argument set types.
        var possibleReturnTypes = new List<(TypeDesignation,Unifier)>();

        //This implementation results in 1 call to each child, regardless of implicit casts
        //Technically it's not sound w.r.t unifier as it allows contradictory bindings, but that's fine, we can go a bit larger here
        //We could remove these contradictions by verifying in CheckUnifications that the argsets unifiers are "consistent with"
        // the computed unifier, but that's actually kind of nontrivial
        var argSetsOrErr = me.Arguments.Select(a => a.PossibleUnifiers(resolver, unifier)).SequenceL();
        if (argSetsOrErr.IsRight)
            return argSetsOrErr.Right;
        var argSets = argSetsOrErr.Left;
        me.GenerateOverloads(argSets);

        var wkArgs = new TypeDesignation[me.Arguments.Count + 1];
        bool _CheckUnifications(bool implicitCasts, Dummy method, int ii, Unifier u) {
            if (ii >= me.Arguments.Count) {
                if (method.Unify(new Dummy(Dummy.METHOD_KEY, wkArgs), u) is { IsLeft: true } unified) {
                    possibleReturnTypes.Add((method.Last.Simplify(unified.Left), unified.Left));
                    return true;
                } else
                    return false;
            } else {
                bool success = false;
                foreach (var (argT, _) in argSets[ii]) {
                    bool argTSuccess = false;
                    if (method.Arguments[ii].Unify(argT, u) is { IsLeft: true} argU) {
                        wkArgs[ii] = argT;
                        argTSuccess |= _CheckUnifications(implicitCasts, method, ii + 1, argU.Left);
                    }
                    //Use implicit casting conservatively *per possible argument type*
                    // eg. If we need T[], an arg presents overloads {string, double[]}, and we have cast T->T[],
                    // then we report string[] and double[], but not double[][],
                    // because double[] sets argTSuccess to true above.
                    if (!argTSuccess && implicitCasts && me.ImplicitParameterCastEnabled(ii)) {
                        var arg = argT.Simplify(u);
                        var mparam = method.Arguments[ii].Simplify(u);
                        var invoke = Dummy.Method(mparam, arg);
                        if (resolver.GetImplicitSources(mparam, out var convs) || resolver.GetImplicitCasts(arg, out convs)) {
                            foreach (var cast in convs) {
                                var cinst = cast.NextInstance;
                                var pu = invoke.Unify(cinst.MethodType, u);
                                if (pu.IsLeft) {
                                    cinst.MarkUsed();
                                    wkArgs[ii] = mparam.Simplify(u);
                                    argTSuccess |= _CheckUnifications(implicitCasts, method, ii + 1, pu.Left);
                                }
                            }
                        }
                    }
                    success |= argTSuccess;
                }
                return success;
            }
        }

        me.RealizableOverloads = new();
        foreach (var m in me.Overloads) {
            wkArgs[^1] = m.Method.Last;
            if (_CheckUnifications(false, m.Method, 0, unifier))
                me.RealizableOverloads.Add(m); //dont simplify
        }
        if (possibleReturnTypes.Count == 0 && me.Arguments.Count.Range().Any(me.ImplicitParameterCastEnabled)) {
            //if normal application doesn't work, check if we can satisfy the method by applying implicit casts
            foreach (var m in me.Overloads) {
                wkArgs[^1] = m.Method.Last;
                if (_CheckUnifications(true, m.Method, 0, unifier))
                    me.RealizableOverloads.Add(m); //dont simplify
            }
        }
        if (possibleReturnTypes.Count > 0)
            return possibleReturnTypes;
        return new TypeUnifyErr.NoPossibleOverload(me, argSets);
    }

    Either<(TypeDesignation, Unifier), TypeUnifyErr> ITypeTree.ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier, bool allowImplicitCast)
        => _ResolveUnifiers(this, resultType, resolver, unifier, allowImplicitCast);
    
    /// <inheritdoc cref="ITypeTree.ResolveUnifiers"/>
    public static Either<(TypeDesignation, Unifier), TypeUnifyErr> _ResolveUnifiers(IMethodTypeTree<T> me, 
        TypeDesignation resultType, TypeResolver resolver, Unifier unifier, bool allowImplicitCast) {
        (Unifier, T, IImplicitTypeConverterInstance?)? result = null;
        List<(TypeDesignation, TypeUnifyErr)> overloadErrs = new();
        if (me.RealizableOverloads == null) me.PossibleUnifiers(resolver, unifier);
        //Only one overload may satisfy the return type, otherwise we fail.
        //If PreferFirstOverload is set, we allow multiple overloads, but use the first.
        for (var im = 0; im < me.RealizableOverloads!.Count; im++) {
            var m = me.RealizableOverloads![im];
            var unified = m.Method.Last.Unify(resultType, unifier);
            if (unified.IsLeft) {
                if (result != null)
                    return new TypeUnifyErr.MultipleOverloads(me, resultType, 
                        result.Value.Item2.Method, m.Method.Simplify(unified.Left));
                result = (unified.Left, m, null);
                if (me.PreferFirstOverload)
                    goto finalize;
            } else
                overloadErrs.Add((m.Method, unified.Right));
        }
        if (result == null && allowImplicitCast) {
            var reqTyp = resultType.Simplify(unifier);
            if (resolver.GetImplicitSourcesList(reqTyp, out var convsl)) {
                for (var im = 0; im < me.RealizableOverloads.Count; im++) {
                    var m = me.RealizableOverloads[im];
                    var invoke = Dummy.Method(reqTyp, m.Method.Last.Simplify(unifier));
                    foreach (var cast in convsl) {
                        var cinst = cast.NextInstance;
                        var unified = cinst.MethodType.Unify(invoke, unifier);
                        if (unified.IsLeft) {
                            if (result != null)
                                return new TypeUnifyErr.MultipleImplicits(me, resultType, 
                                    result.Value.Item2.Method, m.Method.Simplify(unified.Left));
                            cinst.MarkUsed();
                            result = (unified.Left, m, cinst);
                            if (me.PreferFirstOverload)
                                goto finalize;
                        }
                    }
                }
            } else
                for (var im = 0; im < me.RealizableOverloads.Count; im++) {
                    var m = me.RealizableOverloads[im];
                    var mRetTyp = m.Method.Last.Simplify(unifier);
                    if (resolver.GetImplicitCasts(mRetTyp, out var convs)) {
                        var invoke = Dummy.Method(reqTyp, m.Method.Last.Simplify(unifier));
                        foreach (var cast in convs) {
                            var cinst = cast.NextInstance;
                            var unified = cinst.MethodType.Unify(invoke, unifier);
                            if (unified.IsLeft) {
                                if (result != null)
                                    return new TypeUnifyErr.MultipleImplicits(me, resultType, 
                                        result.Value.Item2.Method, m.Method.Simplify(unified.Left));
                                cinst.MarkUsed();
                                result = (unified.Left, m, cinst);
                                if (me.PreferFirstOverload)
                                    goto finalize;
                            }
                        }
                    }
                }
        }
        finalize : ;
        if (result.Try(out var r)) {
            var (u, m, cinst) = r;
            var uerr = me.WillSelectOverload(m, cinst, u);
            if (uerr.IsRight)
                return uerr.Right;
            u = uerr.Left;
            for (int ii = 0; ii < me.Arguments.Count; ++ii) {
                var finArg = me.Arguments[ii].ResolveUnifiers(m.Method.Arguments[ii].Simplify(u), resolver, u, 
                    me.ImplicitParameterCastEnabled(ii));
                if (finArg.IsRight)
                    return finArg.Right;
                u = finArg.Left.Item2;
            }
            return m.Method.Unify(
                    Dummy.Method(cinst == null ? resultType : m.Method.Last, 
                        me.Arguments.Select(a => a.SelectedOverloadReturnType!).ToArray()), u)
                .FMapL(u => {
                    me.SelectedOverload = (m, m.Method.SimplifyDummy(u));
                    me.ImplicitCast = cinst?.Realize(u);
                    return (me.SelectedOverloadReturnType!, u);
                });
        }
        return overloadErrs.Count == 1 ?
            overloadErrs[0].Item2 :
            new TypeUnifyErr.NoResolvableOverload(me, resultType, overloadErrs);
    }

    void ITypeTree.FinalizeUnifiers(Unifier unifier) => _FinalizeUnifiers(this, unifier);
    
    /// <inheritdoc cref="ITypeTree.FinalizeUnifiers"/>
    public static void _FinalizeUnifiers(IMethodTypeTree<T> me, Unifier unifier) {
        if (me.SelectedOverload.Try(out var s))
            me.SelectedOverload = (s.method, s.simplified.SimplifyDummy(unifier));
        me.ImplicitCast = me.ImplicitCast?.Simplify(unifier);
        foreach (var arg in me.Arguments)
            arg.FinalizeUnifiers(unifier);
    }
}

/// <summary>
/// Interface that auto-implements part of <see cref="ITypeTree"/> for ASTs that are not method calls.
/// </summary>
public interface IAtomicTypeTree : ITypeTree {
    /// <summary>
    /// The set of overloads that this object's type can possibly be.
    /// </summary>
    TypeDesignation[] PossibleTypes { get; }
    
    /// <summary>
    /// The type overload selected after <see cref="ITypeTree.ResolveUnifiers"/>.
    /// </summary>
    TypeDesignation? SelectedOverload { get; set; }
    
    TypeDesignation ITypeTree.SelectedOverloadReturnType => 
        ImplicitCast?.ResultType ?? SelectedOverload ?? throw new Exception("Overload not yet finalized");

    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> ITypeTree.PossibleUnifiers(TypeResolver resolver, Unifier unifier)
        => _PossibleUnifiers(resolver, unifier);
    
    /// <inheritdoc cref="ITypeTree.PossibleUnifiers"/>
    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> _PossibleUnifiers(TypeResolver resolver, Unifier unifier) 
        => PossibleTypes.Select(p => (p, unifier)).ToList();

    Either<(TypeDesignation, Unifier), TypeUnifyErr> 
        ITypeTree.ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier, bool allowImplicitCast)
        => _ResolveUnifiers(resultType, resolver, unifier, allowImplicitCast);
    
    /// <inheritdoc cref="ITypeTree.ResolveUnifiers"/>
    Either<(TypeDesignation, Unifier), TypeUnifyErr> 
        _ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier, bool allowImplicitCast) {
        (Unifier, TypeDesignation, IImplicitTypeConverterInstance?)? result = null;
        List<(TypeDesignation, TypeUnifyErr)> overloadErrs = new();
        foreach (var t in PossibleTypes) {
            var unified = t.Unify(resultType, unifier);
            if (unified.IsLeft) {
                if (result != null)
                    return new TypeUnifyErr.MultipleOverloads(this, resultType, 
                        result.Value.Item2, t.Simplify(unified.Left));
                result = (unified.Left, t, null);
            } else
                overloadErrs.Add((t, unified.Right));
        }
        if (result == null && allowImplicitCast) {
            //Cast into resultType if possible, else cast from possibleTypes
            var reqTyp = resultType.Simplify(unifier);
            if (resolver.GetImplicitSources(reqTyp, out var convs)) {
                foreach (var cast in convs)
                foreach (var t in PossibleTypes) {
                    var cinst = cast.NextInstance;
                    var unified = cinst.MethodType.Unify(Dummy.Method(reqTyp, t), unifier);
                    if (unified.IsLeft) {
                        if (result != null)
                            return new TypeUnifyErr.MultipleImplicits(this, resultType, 
                                result.Value.Item2, t.Simplify(unified.Left));
                        cinst.MarkUsed();
                        result = (unified.Left, t, cinst);
                    }
                }
            } else
                foreach (var t in PossibleTypes) {
                    var tTyp = t.Simplify(unifier);
                    if (resolver.GetImplicitCasts(tTyp, out convs)) {
                        foreach (var cast in convs) {
                            var cinst = cast.NextInstance;
                            var unified = cinst.MethodType.Unify(Dummy.Method(reqTyp, tTyp), unifier);
                            if (unified.IsLeft) {
                                if (result != null)
                                    return new TypeUnifyErr.MultipleImplicits(this, resultType, result.Value.Item2, tTyp);
                                cinst.MarkUsed();
                                result = (unified.Left, t, cinst);
                            }
                        }
                    }
                }
        }
        if (result.Try(out var r)) {
            SelectedOverload = r.Item2.Simplify(r.Item1);
            ImplicitCast = r.Item3?.Realize(r.Item1);
            return (SelectedOverloadReturnType!, r.Item1);
        }
        return overloadErrs.Count == 1 ?
            overloadErrs[0].Item2 :
            new TypeUnifyErr.NoResolvableOverload(this, resultType, overloadErrs);
    }
    
    void ITypeTree.FinalizeUnifiers(Unifier unifier) {
        SelectedOverload = SelectedOverload?.Simplify(unifier);
        ImplicitCast = ImplicitCast?.Simplify(unifier);
    }

}


}