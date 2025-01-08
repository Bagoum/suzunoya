using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// A disposable object that nests multiple other disposables.
/// </summary>
[PublicAPI]
public class ListDisposable : IDisposable {
    private readonly IReadOnlyList<IDisposable> disposable;
    private bool disposed = false;

    /// <inheritdoc cref="ListDisposable"/>
    public ListDisposable(IReadOnlyList<IDisposable> tokens) {
        disposable = tokens;
    }

    /// <inheritdoc cref="ListDisposable"/>
    public static ListDisposable From<T>(IEnumerable<T> objects, Func<T, IDisposable> mapper) =>
        new(objects.Select(mapper).ToList());

    /// <inheritdoc/>
    public void Dispose() {
        if (disposed) return;
        disposed = true;
        foreach (var t in disposable)
            t.Dispose();
    }
}
}