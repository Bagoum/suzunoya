using System;
using System.Collections.Generic;
using System.Linq;

namespace BagoumLib.Functional {
public static class Helpers {
    
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