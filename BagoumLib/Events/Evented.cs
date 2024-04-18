using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using BagoumLib.Functional;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace BagoumLib.Events {
/// <summary>
/// A subject that observes elements of type T, maps them to type U, and publishes mapped elements to observers.
/// <br/>New subscribers are sent the existing value as well as any new values.
/// <br/>Always has a current value.
/// </summary>
public class Evented<T, U> : ISubject<T, U>, ICObservable<U> {
    internal U _value;
    private readonly Func<T, U> mapper;
    
    /// <inheritdoc />
    public U Value {
        get => _value;
        set => OnChange.OnNext(this._value = value);
    }
    
    /// <summary>
    /// Event that fires when the value changes. You can subscribe to this directly if you do not want
    ///  to receive the existing value on subscription.
    /// </summary>
    public Event<U> OnChange { get; }
    
    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    public Evented(U val, Func<T, U> mapper) {
        this.mapper = mapper;
        _value = val;
        OnChange = new Event<U>();
        OnChange.OnNext(_value);
    }
    
    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<U> observer) {
        observer.OnNext(_value);
        return OnChange.Subscribe(observer);
    }

    /// <inheritdoc />
    public void OnNext(T value) => Value = mapper(value);

    /// <inheritdoc />
    public void OnError(Exception error) => OnChange.OnError(error);

    /// <inheritdoc />
    public void OnCompleted() => OnChange.OnCompleted();

    /// <summary>
    /// Set a new value only if it is not equal to the existing value.
    /// </summary>
    public void PublishIfNotSame(T value) {
        var next = mapper(value);
        if (!EqualityComparer<U>.Default.Equals(next, _value))
            OnNext(value);
    }

    /// <summary>
    /// Gets <see cref="Value"/>.
    /// </summary>
    public static implicit operator U(Evented<T, U> evo) => evo._value;
}

/// <summary>
/// A wrapper around a value that publishes an event whenever it is set.
/// New subscribers are sent the existing value as well as any new values.
/// <br/>(Same as BehaviorSubject.)
/// <br/>This class can be serialized and deserialized in Newtonsoft.JSON.
/// </summary>
[PublicAPI] [JsonConverter(typeof(EventedSerializer))]
public class Evented<T> : Evented<T, T>, ICSubject<T> {
    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    public Evented(T val) : base(val, x => x) { }
    
    /// <summary>
    /// Gets <see cref="Evented{T,U}.Value"/>.
    /// </summary>
    public static implicit operator T(Evented<T> evo) => evo._value;
}
}