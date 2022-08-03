using System;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {

/// <summary>
/// A disposable that doesn't do anything.
/// </summary>
public class NullDisposable : IDisposable {
    public static readonly NullDisposable Default = new();
    public void Dispose() { }
}
/// <summary>
/// An event that ignores subscriptions and values.
/// </summary>
[PublicAPI]
public class NullEvent<T> : Event<T> {
    public static readonly NullEvent<T> Default = new();
    
    public override void OnNext(T value) {}

    public override IDisposable Subscribe(IObserver<T> observer) => 
        NullDisposable.Default;
}
}