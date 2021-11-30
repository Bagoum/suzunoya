using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
[PublicAPI]
public class ListDisposable : IDisposable {
    private readonly List<IDisposable> disposable;
    private bool disposed = false;

    public ListDisposable(IEnumerable<IDisposable> tokens) {
        disposable = tokens.ToList();
    }

    public static ListDisposable From<T>(IEnumerable<T> objects, Func<T, IDisposable> mapper) =>
        new(objects.Select(mapper));


    public void Dispose() {
        if (disposed) return;
        disposed = true;
        foreach (var t in disposable)
            t.Dispose();
    }
}
}