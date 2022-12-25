using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BagoumLib.DataStructures;
using BagoumLib.Functional;

namespace BagoumLib.Reflection {
/// <summary>
/// An AST that can be used for two-pass type unification with overloading and implicit cast support.
/// </summary>
public interface ITypeTree {
    /// <summary>
    /// First pass, bottom-up: recursively determine all possible types for all operands,
    /// then return the return types of all overloadings that satisfy at least one of the entries
    ///  in the Cartesian product of possible operand sets.
    /// <br/>Implicit casts should be investigated at the *method* level, not the parameter level.
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
        ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier);

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
    public IEnumerable<TypeDesignation.Variable> UnresolvedVariables() {
        if (SelectedOverloadReturnType is TypeDesignation.Variable v)
            yield return v;
    }

    /// <inheritdoc cref="PossibleUnifiers(BagoumLib.Reflection.TypeResolver,BagoumLib.Reflection.Unifier)"/>
    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> PossibleUnifiers() =>
        PossibleUnifiers(new TypeResolver(), Unifier.Empty);
}

/// <summary>
/// Interface that auto-implements part of <see cref="ITypeTree"/> for method call ASTs.
/// </summary>
public interface IMethodTypeTree: ITypeTree {
    /// <summary>
    /// The set of method overloads. All overloads must have the same number of arguments.
    /// </summary>
    TypeDesignation.Dummy[] Overloads { get; }
    /// <summary>
    /// The subset of <see cref="Overloads"/> that can be realized given an initial parse of the provided arguments (ignoring the return type).
    /// Set in <see cref="ITypeTree.PossibleUnifiers(BagoumLib.Reflection.TypeResolver,BagoumLib.Reflection.Unifier)"/>.
    /// </summary>
    List<TypeDesignation.Dummy>? RealizableOverloads { get; set; }
    
    /// <summary>
    /// The set of arguments to the method, whichever of the overload is selected.
    /// </summary>
    IReadOnlyList<ITypeTree> Arguments { get; }
    
    /// <summary>
    /// The method overload selected by <see cref="ITypeTree.ResolveUnifiers"/>.
    /// </summary>
    TypeDesignation.Dummy? SelectedOverload { get; set; }

    TypeDesignation ITypeTree.SelectedOverloadReturnType => 
        ImplicitCast?.ResultType ?? (SelectedOverload ?? throw new Exception("Overload not yet finalized")).Arguments[^1];

    bool ITypeTree.IsFullyResolved => 
        (SelectedOverloadReturnType?.IsResolved ?? false) && Arguments.All(a => a.IsFullyResolved);

    void ITypeTree.FinalizeUnifiers(Unifier unifier) {
        SelectedOverload = SelectedOverload?.SimplifyDummy(unifier);
        ImplicitCast = ImplicitCast?.Simplify(unifier);
        foreach (var arg in Arguments)
            arg.FinalizeUnifiers(unifier);
    }

    IEnumerable<TypeDesignation.Variable> ITypeTree.UnresolvedVariables() {
        if (SelectedOverloadReturnType is TypeDesignation.Variable v)
            yield return v;
        foreach (var cv in Arguments.SelectMany(a => a.UnresolvedVariables()))
            yield return cv;
    }

    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> ITypeTree.
        PossibleUnifiers(TypeResolver resolver, Unifier unifier) => _PossibleOverloads(resolver, unifier);
    
    /// <inheritdoc cref="ITypeTree.PossibleUnifiers(BagoumLib.Reflection.TypeResolver,BagoumLib.Reflection.Unifier)"/>
    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> _PossibleOverloads(TypeResolver resolver, Unifier unifier) {
        //for each method:
        //  for each argument set types in the cartesian product of possible argument types,
        //    including implicit casts from the base argument types,
        //  check if the method can be unified with the argument set types.
        var possibleReturnTypes = new List<(TypeDesignation,Unifier)>();

        //This implementation results in 1 call to each child, regardless of implicit casts
        //Technically it's not sound w.r.t unifier, but that's fine, we can go a bit larger here
        var argSetsOrErr = Arguments.Select(a => a.PossibleUnifiers(resolver, unifier)).SequenceL();
        if (argSetsOrErr.IsRight)
            return argSetsOrErr.Right;
        var argSets = argSetsOrErr.Left;

        var wkArgs = new TypeDesignation[Arguments.Count + 1];
        bool _CheckUnifications(bool implicitCasts, TypeDesignation.Dummy method, int ii, Unifier u) {
            if (ii >= Arguments.Count) {
                if (method.Unify(new TypeDesignation.Dummy("method", wkArgs), u) is { IsLeft: true } unified) {
                    possibleReturnTypes.Add((method.Last.Simplify(unified.Left), unified.Left));
                    return true;
                } else
                    return false;
            } else {
                bool success = false;
                foreach (var (argT, _) in argSets[ii]) {
                    if (method.Arguments[ii].Unify(argT, u) is { IsLeft: true} argU) {
                        wkArgs[ii] = argT;
                        success |= _CheckUnifications(implicitCasts, method, ii + 1, argU.Left);
                    }
                    if (implicitCasts) {
                        var arg = argT.Simplify(u);
                        var mparam = method.Arguments[ii].Simplify(u);
                        var invoke = TypeDesignation.Dummy.Method(mparam, arg);
                        if (resolver.GetImplicitSources(mparam, out var convs) || resolver.GetImplicitCasts(arg, out convs)) {
                            foreach (var cast in convs) {
                                var pu = invoke.Unify(cast.MethodType);
                                if (pu.IsLeft) {
                                    wkArgs[ii] = mparam.Simplify(u);
                                    success |= _CheckUnifications(implicitCasts, method, ii + 1, pu.Left);
                                }
                            }
                        }
                    }
                }
                return success;
            }
        }

        RealizableOverloads = new();
        foreach (var method in Overloads) {
            wkArgs[^1] = method.Last;
            if (_CheckUnifications(false, method, 0, unifier))
                RealizableOverloads.Add(method); //dont simplify
        }
        if (possibleReturnTypes.Count == 0) {
            //if normal application doesn't work, check if we can satisfy the method by applying implicit casts
            foreach (var method in Overloads) {
                wkArgs[^1] = method.Last;
                if (_CheckUnifications(true, method, 0, unifier))
                    RealizableOverloads.Add(method); //dont simplify
            }
        }
        if (possibleReturnTypes.Count > 0)
            return possibleReturnTypes;
        return new TypeUnifyErr.NoOverload(Overloads
                .Select(o => (o as TypeDesignation, null as TypeUnifyErr)).ToArray());
    }

    Either<(TypeDesignation, Unifier), TypeUnifyErr> ITypeTree.ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier)
        => _FinalizeOverload(resultType, resolver, unifier);
    
    /// <inheritdoc cref="ITypeTree.ResolveUnifiers"/>
    Either<(TypeDesignation, Unifier), TypeUnifyErr> 
        _FinalizeOverload(TypeDesignation resultType, TypeResolver resolver, Unifier unifier) {
        (Unifier, TypeDesignation.Dummy, IImplicitTypeConverter?)? result = null;
        List<(TypeDesignation, TypeUnifyErr?)> overloadErrs = new();
        if (RealizableOverloads == null) PossibleUnifiers(resolver, unifier);
        //Only one overload may satisfy the return type, otherwise we fail.
        foreach (var method in RealizableOverloads!) {
            var unified = method.Last.Unify(resultType, unifier);
            if (unified.IsLeft) {
                if (result != null)
                    return new TypeUnifyErr.MultipleOverloads(resultType, result.Value.Item2, method.Simplify(unified.Left));
                result = (unified.Left, method.SimplifyDummy(unified.Left), null);
            } else
                overloadErrs.Add((method, unified.Right));
        }
        if (result == null) {
            var reqTyp = resultType.Simplify(unifier);
            if (resolver.GetImplicitSources(reqTyp, out var convs)) {
                foreach (var method in RealizableOverloads) {
                    var invoke = TypeDesignation.Dummy.Method(reqTyp, method.Last.Simplify(unifier));
                    foreach (var cast in convs) {
                        var unified = cast.MethodType.Unify(invoke);
                        if (unified.IsLeft) {
                            if (result != null)
                                return new TypeUnifyErr.MultipleImplicits(resultType, result.Value.Item2, method.Simplify(unified.Left));
                            result = (unified.Left, method.SimplifyDummy(unified.Left), cast);
                        }
                    }
                }
            } else
                foreach (var method in RealizableOverloads) {
                    var mRetTyp = method.Last.Simplify(unifier);
                    if (resolver.GetImplicitCasts(mRetTyp, out convs)) {
                        var invoke = TypeDesignation.Dummy.Method(reqTyp, method.Last.Simplify(unifier));
                        foreach (var cast in convs) {
                            var unified = cast.MethodType.Unify(invoke);
                            if (unified.IsLeft) {
                                if (result != null)
                                    return new TypeUnifyErr.MultipleImplicits(resultType, result.Value.Item2, method.Simplify(unified.Left));
                                result = (unified.Left, method.SimplifyDummy(unified.Left), cast);
                            }
                        }
                    }
                }
        }

        if (result.Try(out var r)) {
            var (u, overload, cast) = r;
            for (int ii = 0; ii < Arguments.Count; ++ii) {
                var finArg = Arguments[ii].ResolveUnifiers(overload.Arguments[ii].Simplify(u), resolver, u);
                if (finArg.IsRight)
                    return finArg.Right;
                u = finArg.Left.Item2;
            }
            return overload.Unify(
                    new TypeDesignation.Dummy("method", 
                        Arguments.Select(a => a.SelectedOverloadReturnType!)
                            .Append(cast == null ? resultType : overload.Last).ToArray()), u)
                .FMapL(u => {
                    SelectedOverload = overload.SimplifyDummy(u);
                    ImplicitCast = cast?.Realize(u);
                    return (SelectedOverloadReturnType!, u);
                });
        }
        return overloadErrs.Count == 1 ?
            overloadErrs[0].Item2! :
            new TypeUnifyErr.NoOverload(overloadErrs);
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

    void ITypeTree.FinalizeUnifiers(Unifier unifier) {
        SelectedOverload = SelectedOverload?.Simplify(unifier);
        ImplicitCast = ImplicitCast?.Simplify(unifier);
    }

    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> ITypeTree.PossibleUnifiers(TypeResolver resolver, Unifier unifier)
        => _PossibleOverloads(resolver, unifier);
    
    /// <inheritdoc cref="ITypeTree.PossibleUnifiers(BagoumLib.Reflection.TypeResolver,BagoumLib.Reflection.Unifier)"/>
    Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> _PossibleOverloads(TypeResolver resolver, Unifier unifier) 
        => PossibleTypes.Select(p => (p, unifier)).ToList();

    Either<(TypeDesignation, Unifier), TypeUnifyErr> 
        ITypeTree.ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier)
        => _FinalizeOverload(resultType, resolver, unifier);
    
    /// <inheritdoc cref="ITypeTree.ResolveUnifiers"/>
    Either<(TypeDesignation, Unifier), TypeUnifyErr> 
        _FinalizeOverload(TypeDesignation resultType, TypeResolver resolver, Unifier unifier) {
        (Unifier, TypeDesignation, IImplicitTypeConverter?)? result = null;
        List<(TypeDesignation, TypeUnifyErr?)> overloadErrs = new();
        foreach (var t in PossibleTypes) {
            var unified = t.Unify(resultType, unifier);
            if (unified.IsLeft) {
                if (result != null)
                    return new TypeUnifyErr.MultipleOverloads(resultType, result.Value.Item2, t.Simplify(unified.Left));
                result = (unified.Left, t, null);
            } else
                overloadErrs.Add((t, unified.Right));
        }
        if (result == null) {
            //Try implicit casts
            //Cast from resultType if possible, else cast from possibleTypes
            var reqTyp = resultType.Simplify(unifier);
            if (resolver.GetImplicitSources(reqTyp, out var convs)) {
                foreach (var cast in convs)
                foreach (var t in PossibleTypes) {
                    var unified = cast.MethodType.Unify(TypeDesignation.Dummy.Method(reqTyp, t), unifier);
                    if (unified.IsLeft) {
                        if (result != null)
                            return new TypeUnifyErr.MultipleImplicits(resultType, result.Value.Item2, t.Simplify(unified.Left));
                        result = (unified.Left, t, cast);
                    }
                }
            } else
                foreach (var t in PossibleTypes) {
                    var tTyp = t.Simplify(unifier);
                    if (resolver.GetImplicitCasts(tTyp, out convs)) {
                        foreach (var cast in convs) {
                            var unified = cast.MethodType.Unify(TypeDesignation.Dummy.Method(reqTyp, tTyp));
                            if (unified.IsLeft) {
                                if (result != null)
                                    return new TypeUnifyErr.MultipleImplicits(resultType, result.Value.Item2, tTyp);
                                result = (unified.Left, t, cast);
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
            overloadErrs[0].Item2! :
            new TypeUnifyErr.NoOverload(overloadErrs);
    }
}


}