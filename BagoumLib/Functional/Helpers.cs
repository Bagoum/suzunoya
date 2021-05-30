using System;
using System.Collections.Generic;
using System.Linq;

namespace BagoumLib.Functional {
public static class Helpers {
    public static readonly string[] noStrs = { };
    
    public static Errorable<List<T>> Acc<T>(this IEnumerable<Errorable<T>> errbs) {
        var ret = new List<T>();
        var errs = new List<string[]>();
        foreach (var x in errbs) {
            if (errs.Count == 0 && x.Valid) ret.Add(x.Value);
            else if (x.errors.Length > 0) errs.Add(x.errors);
        }
        return errs.Count > 0 ? 
            Errorable<List<T>>.Fail(errs.Join().ToArray()) : 
            ret;
    }

    public static Errorable<List<T>> ReplaceEntries<T>(bool allowFewer, List<T> replaceIn, List<T> replaceFrom, Func<T, bool> replaceFilter) {
        replaceIn = replaceIn.ToList(); //nondestructive
        int jj = 0;
        for (int ii = 0; ii < replaceIn.Count; ++ii) {
            if (replaceFilter(replaceIn[ii])) {
                if (jj < replaceFrom.Count) {
                    replaceIn[ii] = replaceFrom[jj++];
                } else {
                    if (!allowFewer) return Errorable<List<T>>.Fail("Not enough replacements provided");
                }
            }
        }
        if (jj < replaceFrom.Count) return Errorable<List<T>>.Fail("Too many replacements provided");
        return Errorable<List<T>>.OK(replaceIn);
    }

}
}