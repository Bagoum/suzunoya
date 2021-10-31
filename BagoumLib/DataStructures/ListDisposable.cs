using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
[PublicAPI]
public class ListDisposable : IDisposable {
    private readonly List<IDisposable> disposable;

    public ListDisposable(IEnumerable<IDisposable> tokens) {
        disposable = tokens.ToList();
    }

    public static ListDisposable From<T>(IEnumerable<T> objects, Func<T, IDisposable> mapper) =>
        new ListDisposable(objects.Select(mapper));


    public void Dispose() {
        foreach (var t in disposable)
            t.Dispose();
        disposable.Clear();
    }
}
}