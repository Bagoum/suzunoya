using System;
using System.Collections.Generic;

namespace BagoumLib.Cancellation {
/// <summary>
/// Disposal handler for objects with a list of IDisposable tokens.
/// </summary>
public interface ITokenized : IDisposable {
    /// <summary>
    /// Set of disposable tokens to be disposed when <see cref="IDisposable.Dispose"/> is called.
    /// </summary>
    public List<IDisposable> Tokens { get; }

    void IDisposable.Dispose() => Tokens.DisposeAll();
}
}