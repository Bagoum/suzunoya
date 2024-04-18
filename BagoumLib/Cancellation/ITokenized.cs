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

    /// <summary>
    /// Add a token to <see cref="Tokens"/>.
    /// </summary>
    public void AddToken(IDisposable token) => Tokens.Add(token);

    void IDisposable.Dispose() => Tokens.DisposeAll();
}
}