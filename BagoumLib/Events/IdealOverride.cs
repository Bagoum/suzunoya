using System;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {

/// <summary>
/// A wrapper around an ideal value that may be overriden by a temporary value.
/// <see cref="set_Value"/> will set the override.
/// <br/>The ideal value can be accessed as <see cref="Ideal"/>.
/// <br/>Note that the functionality here is very similar to <see cref="OverrideEvented{T}"/>, but
///  this is optimized for the case where there is one ideal value and one override value that may be
///  revoked at will.
/// </summary>
[PublicAPI]
public class IdealOverride<T> : ICSubject<T> {
    public Evented<T> Ideal { get; }
    private Evented<Maybe<T>> overrider { get; } = new(Maybe<T>.None);
    
    private readonly Evented<T> onSet;

    private T ComputeValue() => overrider.Value.Try(out var v) ? v : Ideal;
    public T Value {
        get => onSet.Value;
        set => overrider.Value = value;
    }

    /// <summary>
    /// Reset the override value, so <see cref="get_Value"/> will return the ideal value.
    /// </summary>
    public void RevokeOverride() => overrider.Value = Maybe<T>.None;

    public IdealOverride(T val) {
        Ideal = new(val);
        onSet = new(val);
        _ = Ideal.Subscribe(t => onSet.PublishIfNotSame(ComputeValue()));
        _ = overrider.Subscribe(t => onSet.PublishIfNotSame(ComputeValue()));
    }

    public IDisposable Subscribe(IObserver<T> observer) => onSet.Subscribe(observer);

    
    public void OnCompleted() {
        Ideal.OnCompleted();
    }

    public void OnError(Exception error) {
        Ideal.OnError(error);
    }

    public void OnNext(T value) => Value = value;

    public void SetIdeal(T value) => Ideal.Value = value;
}
}