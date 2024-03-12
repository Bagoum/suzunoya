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
    /// <br/>By default, a method may only check its parameters for implicit casts if required, unless alwaysCheckImplicitCasts is set.
    /// <br/>Note that it is possible to have some outputs that do not actually work, as this may skip some
    ///   parent-child alignment checks for efficiency.
    /// </summary>
    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> PossibleUnifiers(TypeResolver resolver, Unifier unifier, bool alwaysCheckImplicitCasts = false);
    
    /// <summary>
    /// Second pass, top-down: After determining one final type for the entire type tree,
    /// find the one overloading that satisfies that type (if there are 0 or 2+, then return an error),
    /// and unify it with any tree children.
    /// <br/>Implicit casts should be realized at the *parameter* level, not the method level.
    /// <br/>This overloading should be saved locally and its return type must be provided as <see cref="SelectedOverloadReturnType"/>.
    /// </summary>
    Either<(TypeDesignation, Unifier), TypeUnifyErr> 
        ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier, Either<IImplicitTypeConverter, bool> implicitCasts, bool paramImplicitCasts);

    /// <inheritdoc cref="ResolveUnifiers(BagoumLib.Unification.TypeDesignation,BagoumLib.Unification.TypeResolver,BagoumLib.Unification.Unifier,BagoumLib.Functional.Either{BagoumLib.Unification.IImplicitTypeConverter,bool},bool)"/>
    Either<(TypeDesignation, Unifier), TypeUnifyErr>
        ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier) =>
        ResolveUnifiers(resultType, resolver, unifier, new(true), true);

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
    TypeDesignation? SelectedOverloadReturnType => ImplicitCast?.ResultType ?? SelectedOverloadReturnTypeNoCast;
    
    /// <summary>
    /// The return type of the overloading that was selected for this AST.
    /// <br/>Note that for a method call, this must be the return type of the method.
    /// <br/>This should *not* be overriden by <see cref="ImplicitCast"/>.
    /// </summary>
    TypeDesignation? SelectedOverloadReturnTypeNoCast { get; }
    
    /// <summary>
    /// The type to which the return type of this AST is implicitly cast.
    /// </summary>
    public IRealizedImplicitCast? ImplicitCast { get; set; }
    
    /// <summary>
    /// True iff the type of this tree and all its components are fully determined.
    /// <br/>This should only be called after <see cref="ResolveUnifiers(BagoumLib.Unification.TypeDesignation,BagoumLib.Unification.TypeResolver,BagoumLib.Unification.Unifier,BagoumLib.Functional.Either{BagoumLib.Unification.IImplicitTypeConverter,bool},bool)"/>.
    /// </summary>
    public bool IsFullyResolved => SelectedOverloadReturnType?.IsResolved ?? false;

    /// <summary>
    /// Yields all the unbound variables in the selected overload.
    /// </summary>
    public IEnumerable<(Variable, ITypeTree)> UnresolvedVariables() => 
        UnresolvedVariablesInReturnType(this);
    
    /// <summary>
    /// Yields all the unbound variables in the return type of the selected overload.
    /// </summary>
    public static IEnumerable<(Variable, ITypeTree)> UnresolvedVariablesInReturnType(ITypeTree tree) =>
        tree.SelectedOverloadReturnType!.GetVariables().Select(x => (x, tree));
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
public interface IMethodTypeTree : ITypeTree;

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
    void GenerateOverloads(List<(TypeDesignation, Unifier)>[] arguments) { }

    /// <summary>
    /// Whether or not the parameter at `index` can receive implicit casts for the given overload.
    /// <br/>Left: The parameter must match exactly or use the given implicit cast.
    /// <br/>Right: The parameter is allowed or not allowed to use any implicit casts at its level.
    /// </summary>
    Either<IImplicitTypeConverter, bool> ImplicitParameterCast(T overload, int index) => new(true);

    /// <summary>
    /// Set this to true when overlapping overloads all do the same thing.
    ///  For example, the overloads for multiplication are { (float, T)->T, (T, float)->T }.
    ///  These two overloads overlap when T=float, in which case the overloads are equivalent.
    /// <br/>This will cause typechecking in <see cref="ITypeTree.PossibleUnifiers"/> and 
    ///  <see cref="ITypeTree.ResolveUnifiers(BagoumLib.Unification.TypeDesignation,BagoumLib.Unification.TypeResolver,BagoumLib.Unification.Unifier,BagoumLib.Functional.Either{BagoumLib.Unification.IImplicitTypeConverter?,bool},bool)"/> to complete as soon as a single valid overload is found.
    /// </summary>
    bool OverloadsAreInterchangeable => false;
    
    /// <summary>
    /// The subset of <see cref="Overloads"/> that can be realized given an initial parse of the provided arguments (ignoring the return type).
    /// Set in <see cref="ITypeTree.PossibleUnifiers"/>.
    /// </summary>
    List<T>? RealizableOverloads { get; set; }
    
    /// <summary>
    /// The set of arguments to the method, whichever of the overloads is selected.
    /// </summary>
    IReadOnlyList<ITypeTree> Arguments { get; }
    
    /// <summary>
    /// The method overload selected by <see cref="ITypeTree.ResolveUnifiers(BagoumLib.Unification.TypeDesignation,BagoumLib.Unification.TypeResolver,BagoumLib.Unification.Unifier,BagoumLib.Functional.Either{BagoumLib.Unification.IImplicitTypeConverter?,bool},bool)"/>.
    /// </summary>
    (T method, Dummy simplified)? SelectedOverload { get; set; }

    /// <summary>
    /// Called in <see cref="ITypeTree.ResolveUnifiers(BagoumLib.Unification.TypeDesignation,BagoumLib.Unification.TypeResolver,BagoumLib.Unification.Unifier,BagoumLib.Functional.Either{BagoumLib.Unification.IImplicitTypeConverter?,bool},bool)"/> when the overload is selected,
    ///  and before arguments are unified against the overload.
    /// <br/>This generally requires no implementation, except when overloads or casts carry implicit
    ///  variable declarations.
    /// </summary>
    Either<Unifier, TypeUnifyErr> WillSelectOverload(T method, IImplicitTypeConverterInstance? cast, Unifier u) => u;
    
    TypeDesignation? ITypeTree.SelectedOverloadReturnTypeNoCast => SelectedOverload?.simplified.Last;

    bool ITypeTree.IsFullyResolved => 
        (SelectedOverloadReturnType?.IsResolved ?? false) && Arguments.All(a => a.IsFullyResolved);
    
    IEnumerable<(Variable, ITypeTree)> ITypeTree.UnresolvedVariables() =>
        Arguments.SelectMany(a => a.UnresolvedVariables()).Concat(UnresolvedVariablesInReturnType(this));

    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> ITypeTree.
        PossibleUnifiers(TypeResolver resolver, Unifier unifier, bool alwaysCheckImplicitCasts) => 
        _PossibleUnifiers(this, resolver, unifier, alwaysCheckImplicitCasts);
    
    /// <inheritdoc cref="ITypeTree.PossibleUnifiers"/>
    public static Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> 
        _PossibleUnifiers(IMethodTypeTree<T> me, TypeResolver resolver, Unifier unifier, bool alwaysCheckImplicitCasts) {
        //for each method:
        //  for each argument set types in the cartesian product of possible argument types,
        //    including implicit casts from the base argument types,
        //  check if the method can be unified with the argument set types.

        //This implementation results in 1-2 calls to each child, regardless of implicit casts
        //Technically it's not sound w.r.t unifier as it allows contradictory bindings, but that's fine, we can go a bit larger here
        //We could remove these contradictions by verifying in CheckUnifications that the argsets unifiers are "consistent with"
        // the computed unifier, but that's actually kind of nontrivial.
        var baseUnifier = unifier;
        var argSets = new List<(TypeDesignation, Unifier)>[me.Arguments.Count];
        TypeUnifyErr? LoadArgSets(bool forceImplicits) {
            unifier = baseUnifier;
            for (int ii = 0; ii < me.Arguments.Count; ++ii) {
                var argUnifiers = me.Arguments[ii].PossibleUnifiers(resolver, unifier, forceImplicits);
                if (!argUnifiers.TryL(out var res))
                    return argUnifiers.Right;
                argSets[ii] = res;
                //If there's only one possible binding for the child, then we use that information, otherwise we discard it
                if (res.Count == 1)
                    unifier = res[0].Item2;
            }
            me.GenerateOverloads(argSets);
            return null;
        }
        if (LoadArgSets(false) is { } err)
            return err;

        var possibleReturnTypes = new List<(TypeDesignation,Unifier)>();
        var wkArgs = new TypeDesignation[me.Arguments.Count + 1];
        CheckOverloads();
        if (possibleReturnTypes.Count > 0)
            return possibleReturnTypes;
        var origArgSets = argSets.ToList();
        //Handles some cases where methods at lower levels of the tree returned only types that didn't require
        // implicit casts, even though this higher part of the tree imposes a requirement that would cause an implicit cast
        if (LoadArgSets(true) is null) {
            CheckOverloads();
            if (possibleReturnTypes.Count > 0)
                return possibleReturnTypes;
        }
        return new TypeUnifyErr.NoPossibleOverload(me, origArgSets);
        
        bool CheckUnifications(bool implicitCasts, T overload, int ii, Unifier u) {
            var method = overload.Method;
            if (ii >= me.Arguments.Count) {
                if (method.Unify(new Dummy(Dummy.METHOD_KEY, wkArgs), u).TryL(out var unified)) {
                    possibleReturnTypes.Add((method.Last.Simplify(unified), unified));
                    return true;
                } else
                    return false;
            } else {
                bool success = false;
                var impCast = me.ImplicitParameterCast(overload, ii);
                //By default, try to unify this argument index without implicit casts or
                // with only using the explicitly-provided cast
                for (int jj = 0; jj < argSets[ii].Count; ++jj) {
                    var argT = argSets[ii][jj].Item1;
                    var explicitCastSuccess = false;
                    if (impCast.TryL(out var reqCast)) {
                        var cinst = reqCast.NextInstance;
                        var invoke = Dummy.Method(method.Arguments[ii].Simplify(u), argT.Simplify(u));
                        if (invoke.Unify(cinst.MethodType, u).TryL(out var reqCastU)) {
                            cinst.MarkUsed();
                            wkArgs[ii] = invoke.Last;
                            success |= (explicitCastSuccess = 
                                CheckUnifications(implicitCasts, overload, ii + 1, reqCastU));
                        }
                    }
                    if (!explicitCastSuccess && method.Arguments[ii].Unify(argT, u).TryL(out var argU)) {
                        wkArgs[ii] = argT;
                        success |= CheckUnifications(implicitCasts, overload, ii + 1, argU);
                    } 
                }
                if (success || !implicitCasts || impCast is not { IsRight: true, Right: true })
                    return success;
                //If no possible value matches, then use implicit casts
                for (int jj = 0; jj < argSets[ii].Count; ++jj) {
                    var argT = argSets[ii][jj].Item1;
                    var arg = argT.Simplify(u);
                    var mparam = method.Arguments[ii].Simplify(u);
                    var invoke = Dummy.Method(mparam, arg);
                    if (resolver.GetImplicitSources(mparam, out var convs) || resolver.GetImplicitCasts(arg, out convs)) {
                        foreach (var cast in convs) {
                            if (!cast.SourceAllowed(invoke.Arguments[0]))
                                continue;
                            var cinst = cast.NextInstance;
                            var pu = invoke.Unify(cinst.MethodType, u);
                            if (pu.IsLeft) {
                                cinst.MarkUsed();
                                wkArgs[ii] = invoke.Last;
                                success |= CheckUnifications(implicitCasts, overload, ii + 1, pu.Left);
                            }
                        }
                    }
                }
                return success;
            }
        }
        void CheckOverloads() {
            me.RealizableOverloads = new();
            for (int im = 0; im < me.Overloads.Count; ++im) {
                var m = me.Overloads[im];
                wkArgs[^1] = m.Method.Last;
                if (CheckUnifications(false, m, 0, unifier)) {
                    me.RealizableOverloads.Add(m); //dont simplify
                    if (me.OverloadsAreInterchangeable)
                        break;
                }
            }
            if (possibleReturnTypes.Count == 0 || alwaysCheckImplicitCasts) {
                for (int im = 0; im < me.Overloads.Count; ++im) {
                    var m = me.Overloads[im];
                    if (me.RealizableOverloads.Contains(m))
                        continue;
                    for (int ii = 0; ii < me.Arguments.Count; ++ii)
                        if (me.ImplicitParameterCast(m, ii).TryR(out var allowed) && allowed)
                            goto check_implicit_casts;
                    continue;
                    check_implicit_casts:
                    wkArgs[^1] = m.Method.Last;
                    if (CheckUnifications(true, m, 0, unifier)) {
                        me.RealizableOverloads.Add(m); //dont simplify
                        if (me.OverloadsAreInterchangeable)
                            break;
                    }
                }
            }
        }
    }

    Either<(TypeDesignation, Unifier), TypeUnifyErr> ITypeTree.ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier, 
        Either<IImplicitTypeConverter, bool> implicitCasts, bool paramImplicitCasts)
        => _ResolveUnifiers(this, resultType, resolver, unifier, implicitCasts, paramImplicitCasts);
    
    /// <inheritdoc cref="ITypeTree.ResolveUnifiers(BagoumLib.Unification.TypeDesignation,BagoumLib.Unification.TypeResolver,BagoumLib.Unification.Unifier,BagoumLib.Functional.Either{BagoumLib.Unification.IImplicitTypeConverter,bool},bool)"/>
    public static Either<(TypeDesignation, Unifier), TypeUnifyErr> _ResolveUnifiers(IMethodTypeTree<T> me, 
        TypeDesignation resultType, TypeResolver resolver, Unifier unifier, 
        Either<IImplicitTypeConverter, bool> implicitCasts, bool paramImplicitCasts) {
        ((TypeDesignation, Unifier), T)? result = null;
        List<(TypeDesignation, TypeUnifyErr)> overloadErrs = new();
        List<(TypeDesignation, TypeUnifyErr)> finalizeErrs = new();
        List<(TypeDesignation, TypeUnifyErr)>? noImplicitFinalizeErrs = null;
        if (me.RealizableOverloads == null) me.PossibleUnifiers(resolver, unifier);
        var reqTyp = resultType.Simplify(unifier);
        if (TryUseProvidedImplicit(false) is { } eret)
            return eret;
        if (TryUseAnyDirect(false) is { } dret)
            return dret;
        if (TryUseAnyImplicit(false) is { } ret)
            return ret;

        //If no method return types matched, or parameter casts are disabled, then parameter casts don't matter.
        if (finalizeErrs.Count == 0 || !paramImplicitCasts)
            goto fail;
        //If we have too many overloads when child casts are disabled, then we can't get any fewer
        // by enabling parameter casts. (At least in the standard case...?)
        for (int ii = 0; ii < finalizeErrs.Count; ++ii)
            if (finalizeErrs[ii].Item2 is TypeUnifyErr.MultipleImplicits or TypeUnifyErr.MultipleOverloads)
                goto fail;
        //Otherwise, prefer error reporting with implicit casts enabled.
        noImplicitFinalizeErrs = finalizeErrs.ToList();
        finalizeErrs.Clear();
        overloadErrs.Clear();
        
        if (TryUseProvidedImplicit(true) is { } eretimp)
            return eretimp;
        if (TryUseAnyDirect(true) is { } dretimp)
            return dretimp;
        if (TryUseAnyImplicit(true) is { } retimp)
            return retimp;
        
        fail:
        if (finalizeErrs.Count > 0)
            return finalizeErrs.Count == 1 ?
                finalizeErrs[0].Item2 :
                new TypeUnifyErr.NoResolvableOverload(me, resultType, finalizeErrs);
        if (noImplicitFinalizeErrs?.Count > 0)
            return noImplicitFinalizeErrs.Count == 1 ?
                noImplicitFinalizeErrs[0].Item2 :
                new TypeUnifyErr.NoResolvableOverload(me, resultType, noImplicitFinalizeErrs);
        return overloadErrs.Count == 1 ?
            overloadErrs[0].Item2 :
            new TypeUnifyErr.NoResolvableOverload(me, resultType, overloadErrs);

        Either<(TypeDesignation, Unifier), TypeUnifyErr>? TryUseAnyDirect(bool allowChildCasts) {
            for (var im = 0; im < me.RealizableOverloads!.Count; im++) {
                var m = me.RealizableOverloads![im];
                var unified = m.Method.Last.Unify(resultType, unifier);
                if (unified.IsLeft) {
                    var attempt_ = TryFinalize((unified.Left, m, null), allowChildCasts);
                    if (!attempt_.Try(out var attempt))
                        continue;
                    if (attempt.IsRight)
                        finalizeErrs.Add((m.Method, attempt.Right));
                    else {
                        if (result != null)
                            return new TypeUnifyErr.MultipleOverloads(me, resultType,
                                result.Value.Item2.Method, m.Method.Simplify(unified.Left));
                        result = (attempt.Left, m);
                        if (me.OverloadsAreInterchangeable)
                            return result.Value.Item1;
                    }
                } else
                    overloadErrs.Add((m.Method, unified.Right));
            }
            if (result.Try(out var r))
                return r.Item1;
            else return null;
        }
        Either<(TypeDesignation, Unifier), TypeUnifyErr>? TryUseImplicit(T m, Dummy invoke, IImplicitTypeConverter cast, bool allowChildCasts) {
            if (!cast.SourceAllowed(invoke.Arguments[0]))
                return null;
            var cinst = cast.NextInstance;
            var unified = cinst.MethodType.Unify(invoke, unifier);
            if (unified.IsLeft) {
                cinst.MarkUsed();
                var attempt_ = TryFinalize((unified.Left, m, cinst), allowChildCasts);
                if (!attempt_.Try(out var attempt))
                    return null;
                if (attempt.IsRight)
                    finalizeErrs.Add((m.Method, attempt.Right));
                else {
                    if (result != null)
                        return new TypeUnifyErr.MultipleImplicits(me, resultType,
                            result.Value.Item2.Method, m.Method.Simplify(unified.Left));
                    result = (attempt.Left, m);
                    if (me.OverloadsAreInterchangeable)
                        return result.Value.Item1;
                }
            }
            return null;
        }
        Either<(TypeDesignation, Unifier), TypeUnifyErr>? TryUseProvidedImplicit(bool allowChildCasts) {
            if (implicitCasts.TryL(out var reqCast)) {
                for (var im = 0; im < me.RealizableOverloads!.Count; im++) {
                    var m = me.RealizableOverloads[im];
                    var invoke = Dummy.Method(reqTyp, m.Method.Last.Simplify(unifier));
                    if (TryUseImplicit(m, invoke, reqCast, allowChildCasts) is { } ret)
                        return ret;
                    
                }
            }
            if (result.Try(out var r))
                return r.Item1;
            else return null;
        }
        
        Either<(TypeDesignation, Unifier), TypeUnifyErr>? TryUseAnyImplicit(bool allowChildCasts) {
            if (implicitCasts is { IsRight: true, Right: true }) {
                if (resolver.GetImplicitSourcesList(reqTyp, out var convsl)) {
                    for (var im = 0; im < me.RealizableOverloads!.Count; im++) {
                        var m = me.RealizableOverloads[im];
                        var invoke = Dummy.Method(reqTyp, m.Method.Last.Simplify(unifier));
                        foreach (var cast in convsl) {
                            if (TryUseImplicit(m, invoke, cast, allowChildCasts) is { } ret)
                                return ret;
                        }
                    }
                } else
                    for (var im = 0; im < me.RealizableOverloads!.Count; im++) {
                        var m = me.RealizableOverloads[im];
                        var mRetTyp = m.Method.Last.Simplify(unifier);
                        if (resolver.GetImplicitCasts(mRetTyp, out var convs)) {
                            var invoke = Dummy.Method(reqTyp, m.Method.Last.Simplify(unifier));
                            foreach (var cast in convs) {
                                if (TryUseImplicit(m, invoke, cast, allowChildCasts) is { } ret)
                                    return ret;
                            }
                        }
                    }
            }
            if (result.Try(out var r))
                return r.Item1;
            else return null;
        }
        Either<(TypeDesignation, Unifier), TypeUnifyErr>? TryFinalize((Unifier, T, IImplicitTypeConverterInstance?) r, bool allowChildCasts) {
            var (u, m, cinst) = r;
            Either<IImplicitTypeConverter, bool> ParamCast(int ii) {
                var cast = me.ImplicitParameterCast(m, ii);
                if (cast.IsRight)
                    cast = new(cast.Right && allowChildCasts);
                return cast;
            }
            var uerr = me.WillSelectOverload(m, cinst, u);
            if (uerr.IsRight)
                return uerr.Right;
            u = uerr.Left;
            //We switch between forwards and backwards iteration until all arguments are unified.
            var (start, end) = (0, me.Arguments.Count - 1);
            var forward = true;
            bool failed = false;
            while (end >= start) {
                if (forward) {
                    var res = me.Arguments[start].ResolveUnifiers(m.Method.Arguments[start].Simplify(u), resolver, u,
                        ParamCast(start), allowChildCasts);
                    if (res.IsLeft) {
                        failed = false;
                        u = res.Left.Item2;
                        ++start;
                    } else if (res.Right is TypeUnifyErr.MultipleImplicits or TypeUnifyErr.MultipleOverloads &&
                               !failed) {
                        failed = true;
                        forward = false;
                    } else {
                        return res.Right;
                    }
                }
                if (!forward) {
                    var res = me.Arguments[end].ResolveUnifiers(m.Method.Arguments[end].Simplify(u), resolver, u,
                        ParamCast(end), allowChildCasts);
                    if (res.IsLeft) {
                        failed = false;
                        u = res.Left.Item2;
                        --end;
                    } else if (res.Right is TypeUnifyErr.MultipleImplicits or TypeUnifyErr.MultipleOverloads &&
                               !failed) {
                        failed = true;
                        forward = true;
                    } else {
                        return res.Right;
                    }
                        
                }
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
    }

    void ITypeTree.FinalizeUnifiers(Unifier unifier) => _FinalizeUnifiers(this, unifier);
    
    /// <inheritdoc cref="ITypeTree.FinalizeUnifiers"/>
    public static void _FinalizeUnifiers(IMethodTypeTree<T> me, Unifier unifier) {
        if (me.SelectedOverload.Try(out var s))
            me.SelectedOverload = (s.method, s.simplified.SimplifyDummy(unifier));
        me.ImplicitCast = me.ImplicitCast?.Simplify(unifier);
        for (int ii = 0; ii < me.Arguments.Count; ++ii)
            me.Arguments[ii].FinalizeUnifiers(unifier);
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
    /// The type overload selected after <see cref="ITypeTree.ResolveUnifiers(BagoumLib.Unification.TypeDesignation,BagoumLib.Unification.TypeResolver,BagoumLib.Unification.Unifier,BagoumLib.Functional.Either{BagoumLib.Unification.IImplicitTypeConverter?,bool},bool)"/>.
    /// </summary>
    TypeDesignation? SelectedOverload { get; set; }
    
    /// <summary>
    /// Called in <see cref="ITypeTree.ResolveUnifiers(BagoumLib.Unification.TypeDesignation,BagoumLib.Unification.TypeResolver,BagoumLib.Unification.Unifier,BagoumLib.Functional.Either{BagoumLib.Unification.IImplicitTypeConverter?,bool},bool)"/> when the overload is selected.
    /// <br/>This generally requires no implementation, except when overloads or casts carry implicit
    ///  variable declarations.
    /// </summary>
    Either<Unifier, TypeUnifyErr> WillSelectOverload(TypeDesignation overload, IImplicitTypeConverterInstance? cast, Unifier u) => u;
    
    TypeDesignation? ITypeTree.SelectedOverloadReturnTypeNoCast => SelectedOverload;

    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> ITypeTree.PossibleUnifiers(TypeResolver resolver, Unifier unifier, bool alwaysCheckImplicitCasts)
        => _PossibleUnifiers(resolver, unifier); //atomics do not have anything to check implicit casts over
    
    /// <inheritdoc cref="ITypeTree.PossibleUnifiers"/>
    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> _PossibleUnifiers(TypeResolver resolver, Unifier unifier) 
        => PossibleTypes.Select(p => (p.Simplify(unifier), unifier)).ToList();

    Either<(TypeDesignation, Unifier), TypeUnifyErr> 
        ITypeTree.ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier, 
            Either<IImplicitTypeConverter, bool> implicitCasts, bool paramImplicitCasts)
        => _ResolveUnifiers(this, resultType, resolver, unifier, implicitCasts, paramImplicitCasts);
    
    /// <inheritdoc cref="ITypeTree.ResolveUnifiers(BagoumLib.Unification.TypeDesignation,BagoumLib.Unification.TypeResolver,BagoumLib.Unification.Unifier,BagoumLib.Functional.Either{BagoumLib.Unification.IImplicitTypeConverter?,bool},bool)"/>
    public static Either<(TypeDesignation, Unifier), TypeUnifyErr> 
        _ResolveUnifiers(IAtomicTypeTree me, TypeDesignation resultType, TypeResolver resolver, Unifier unifier, 
            Either<IImplicitTypeConverter, bool> implicitCasts, bool paramImplicitCasts) {
        (Unifier, TypeDesignation, IImplicitTypeConverterInstance?)? result = null;
        List<(TypeDesignation, TypeUnifyErr)> overloadErrs = new();
        var reqTyp = resultType.Simplify(unifier);
        //Use the explicitly-provided cast if available
        if (implicitCasts.TryL(out var reqCast)) {
            foreach (var t in me.PossibleTypes) {
                if (TryUseImplicit(t, Dummy.Method(reqTyp, t), reqCast) is
                    { } ret)
                    return ret;
            }
        }
        if (result.HasValue) goto finalize;
        //Then try direct matching
        foreach (var t in me.PossibleTypes) {
            var unified = t.Unify(resultType, unifier);
            if (unified.IsLeft) {
                result = (unified.Left, t.Simplify(unified.Left), null);
                goto finalize;
            } else
                overloadErrs.Add((t, unified.Right));
        }
        //Then match general implicits if permitted
        if (result == null && implicitCasts is { IsRight: true, Right: true }) {
            if (resolver.GetImplicitSources(reqTyp, out var convs)) {
                foreach (var cast in convs)
                foreach (var t in me.PossibleTypes) {
                    if (TryUseImplicit(t, Dummy.Method(reqTyp, t), cast) is
                        { } ret)
                        return ret;
                }
            } else
                foreach (var t in me.PossibleTypes) {
                    var tTyp = t.Simplify(unifier);
                    if (resolver.GetImplicitCasts(tTyp, out convs)) {
                        foreach (var cast in convs) {
                            if (TryUseImplicit(t, Dummy.Method(reqTyp, tTyp), cast) is { } ret)
                                return ret;
                        }
                    }
                }
        }
        finalize:
        if (result.Try(out var r)) {
            var overload = r.Item2.Simplify(r.Item1);
            var u = me.WillSelectOverload(overload, r.Item3, r.Item1);
            if (u.IsRight)
                return u.Right;
            me.SelectedOverload = overload;
            me.ImplicitCast = r.Item3?.Realize(u.Left);
            return (me.SelectedOverloadReturnType!, u.Left);
        }
        return overloadErrs.Count == 1 ?
            overloadErrs[0].Item2 :
            new TypeUnifyErr.NoResolvableOverload(me, resultType, overloadErrs);
        
        Either<(TypeDesignation, Unifier), TypeUnifyErr>? TryUseImplicit(TypeDesignation t, Dummy invoke, IImplicitTypeConverter cast) {
            if (!cast.SourceAllowed(t))
                return null;
            var cinst = cast.NextInstance;
            var unified = cinst.MethodType.Unify(invoke, unifier);
            if (unified.IsLeft) {
                if (result != null)
                    return new TypeUnifyErr.MultipleImplicits(me, resultType, 
                        result.Value.Item2, t.Simplify(unified.Left));
                cinst.MarkUsed();
                result = (unified.Left, t.Simplify(unified.Left), cinst);
            }
            return null;
        }
    }
    
    void ITypeTree.FinalizeUnifiers(Unifier unifier) {
        SelectedOverload = SelectedOverload?.Simplify(unifier);
        ImplicitCast = ImplicitCast?.Simplify(unifier);
    }

}


}