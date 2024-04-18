using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// An event wrapper around a zero-argument function whose value may change unpredictably.
/// <br/>This wrapper will only publish return values of the wrapped function when
///  one of the provided triggers is updated or <see cref="Recompute"/> is called.
/// <br/>If the value does not change, this wrapper will not publish anything.
/// </summary>
[PublicAPI]
public class LazyEvented<T> : ICSubject<T> {
    private T _value;
    private readonly Func<T> getter;
    
    /// <inheritdoc/>
    public T Value {
        get => _value;
        set => onSet.OnNext(this._value = value);
    }
    
    private readonly Event<T> onSet;
    private readonly List<IDisposable> tokens = new();

    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    /// <param name="val">Function to lazily evaluate to obtain a value</param>
    /// <param name="triggers">Observables to use as triggers for function reealuation</param>
    public LazyEvented(Func<T> val, params IObservable<Unit>[] triggers) {
        _value = (this.getter = val)();
        onSet = new Event<T>();
        foreach (var t in triggers)
            tokens.Add(t.Subscribe(_ => Recompute()));
        Recompute();
    }

    /// <summary>
    /// Get the value of this event.
    /// </summary>
    public static implicit operator T(LazyEvented<T> evo) => evo._value;
    
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer) {
        observer.OnNext(_value);
        return (onSet ?? throw new Exception("Computed event not initialized")).Subscribe(observer);
    }

    /// <summary>
    /// Explicitly recompute the value of the function.
    /// </summary>
    public void Recompute() {
        var nv = getter();
        if (!EqualityComparer<T>.Default.Equals(nv, _value))
            OnNext(nv);
    }

    /// <inheritdoc/>
    public void OnNext(T value) => Value = value;

    /// <inheritdoc/>
    public void OnError(Exception error) => onSet.OnError(error);

    /// <inheritdoc/>
    public void OnCompleted() => onSet.OnCompleted();
}
}