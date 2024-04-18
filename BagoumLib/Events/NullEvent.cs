using System;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {

/// <summary>
/// A disposable that doesn't do anything.
/// </summary>
public class NullDisposable : IDisposable {
    /// <summary>
    /// Singleton instance of <see cref="NullDisposable"/>.
    /// </summary>
    public static readonly NullDisposable Default = new();
    
    /// <inheritdoc/>
    public void Dispose() { }
}
/// <summary>
/// An event that ignores subscriptions and values.
/// </summary>
[PublicAPI]
public class NullEvent<T> : Event<T> {
    /// <summary>
    /// Singleton instance of <see cref="NullEvent{T}"/>.
    /// </summary>
    public static readonly NullEvent<T> Default = new();
    
    /// <inheritdoc/>
    public override void OnNext(T value) {}

    /// <inheritdoc/>
    public override IDisposable Subscribe(IObserver<T> observer) => 
        NullDisposable.Default;
}
}