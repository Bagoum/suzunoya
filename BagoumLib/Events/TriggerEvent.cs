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
public class TriggerEvent<T> : Event<T> {
    private bool OnNextAllowed { get; set; } = true;

    /// <inheritdoc/>
    public override void OnNext(T value) {
        if (!OnNextAllowed) return;
        OnNextAllowed = false;
        base.OnNext(value);
    }

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