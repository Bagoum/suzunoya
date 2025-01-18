using System;
using System.Collections.Generic;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {

/// <summary>
/// An event that will only dispatch listeners the first time it receives
///  an OnNext call, and then noop until it is reset.
/// </summary>
[PublicAPI]
public class TriggerEvent<T> : IBSubject<T> {
    /// <inheritdoc/>
    public bool HasValue => ev.HasValue;
    /// <inheritdoc/>
    public T Value => ev.Value;
    
    private bool OnNextAllowed { get; set; } = true;
    
    private readonly Event<T> ev = new();

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer) => ev.Subscribe(observer);

    /// <inheritdoc/>
    public void OnNext(T value) {
        if (!OnNextAllowed) return;
        OnNextAllowed = false;
        ev.OnNext(value);
    }

    /// <inheritdoc/>
    public void OnCompleted() => ev.OnCompleted();

    /// <inheritdoc/>
    public void OnError(Exception error) => ev.OnError(error);

    /// <summary>
    /// Reset the trigger so it can be called again.
    /// </summary>
    public void Reset() {
        OnNextAllowed = true;
    }

    /// <summary>
    /// When the provided observable sends a value, this trigger will be reset.
    /// </summary>
    public IDisposable ResetOn<R>(IObservable<R> resetter) =>
        resetter.Subscribe(_ => Reset());

}
}