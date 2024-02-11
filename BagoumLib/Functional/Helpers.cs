using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace BagoumLib.Functional {
/// <summary>
/// Helpers and extensions for functional types.
/// </summary>
[PublicAPI]
public static class Helpers {
    /// <summary>
    /// If the Either is a value (left), return it. If the Either is a delayed value (right), resolve it.
    /// </summary>
    public static T Resolve<T>(this Either<T, Func<T>> valueOrDelayed) {
        if (valueOrDelayed.IsLeft)
            return valueOrDelayed.Left;
        else
            return valueOrDelayed.Right();
    }
    
    /// <summary>
    /// When both sides of an Either derive from a base type, return the left or right value as the base type.
    /// </summary>
    public static T LeftOrRight<L,R,T>(this Either<L, R> either) where L: T where R: T => 
        either.IsLeft ? either.Left : either.Right;


    /// <summary>
    /// Get the value if it is present, otherwise get null.
    /// </summary>
    public static T? ValueOrSNull<T>(this Maybe<T> m) where T: struct => m.Valid ? m.Value : null;
    
    /// <summary>
    /// Get the value if it is present, otherwise get null.
    /// </summary>
    public static T? ValueOrNull<T>(this Maybe<T> m) where T: class => m.Valid ? m.Value : null;
    
    /// <summary>
    /// Accumulate many results of a selection over a failable function together.
    /// If at least one Either is Right, then the result will short-circuit and return that Right.
    /// </summary>
    public static Either<List<L>, R> SequenceL<T, L, R>(this IEnumerable<T> items, Func<T, Either<L, R>> map) {
        List<L> l = new();
        foreach (var item in items) {
            var x = map(item);
            if (x.IsRight) {
                return x.Right;
            } else
                l.Add(x.Left);
        }
        return l;
    }

    /// <summary>
    /// Accumulate many Eithers together.
    /// If at least one Either is Right, then the result will short-circuit and return that Right.
    /// </summary>
    public static Either<List<L>, R> SequenceL<L, R>(this IEnumerable<Either<L, R>> eithers) {
        List<L> l = new();
        foreach (var x in eithers) {
            if (x.IsRight) {
                return x.Right;
            } else
                l.Add(x.Left);
        }
        return l;
    }
    
    /// <summary>
    /// Accumulate many Eithers together.
    /// If at least one Either is Right, then the result will be Right.
    /// Else the result will be a Left (including in the case when no arguments are provided.)
    /// </summary>
    public static Either<List<L>, List<R>> AccFailToR<L, R>(this IEnumerable<Either<L, R>> eithers) {
        List<L>? l = null;
        List<R>? r = null;
        foreach (var x in eithers) {
            if (x.IsRight) {
                (r??=new()).Add(x.Right);
            } else if (r is null)
                (l??=new()).Add(x.Left);
        }
        return r is null ?
            l ?? new() :
            r;
    }
    
    /// <summary>
    /// Accumulate many Eithers together.
    /// If at least one Either is Right, then the result will be Right.
    /// Else the result will be a Left (including in the case when no arguments are provided.)
    /// </summary>
    public static Either<List<L>, List<R>> AccFailToR<L, R>(this IEnumerable<Either<L, List<R>>> eithers) {
        List<L>? l = null;
        List<R>? r = null;
        foreach (var x in eithers) {
            if (x.IsRight) {
                (r??=new()).AddRange(x.Right);
            } else if (r is null)
                (l??=new()).Add(x.Left);
        }
        return r is null ?
            l ?? new() :
            r;
    }
    

    public static Either<List<T>, string> ReplaceEntries<T>(bool allowFewer, List<T> replaceIn, List<T> replaceFrom, Func<T, bool> replaceFilter) {
        replaceIn = replaceIn.ToList(); //nondestructive
        int jj = 0;
        for (int ii = 0; ii < replaceIn.Count; ++ii) {
            if (replaceFilter(replaceIn[ii])) {
                if (jj < replaceFrom.Count) {
                    replaceIn[ii] = replaceFrom[jj++];
                } else {
                    if (!allowFewer) return "Not enough replacements provided";
                }
            }
        }
        if (jj < replaceFrom.Count) return "Too many replacements provided";
        return replaceIn;
    }

}
}