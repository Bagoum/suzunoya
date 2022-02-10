using System;
using System.Reactive.Subjects;
using BagoumLib.Functional;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace BagoumLib.Events {
/// <summary>
/// A wrapper around a value that publishes an event whenever it is set.
/// New subscribers are sent the existing value as well as any new values.
/// <br/>(Same as BehaviorSubject.)
/// <br/>This class can be serialized and deserialized in Newtonsoft.JSON.
/// </summary>
[PublicAPI] [JsonConverter(typeof(EventedSerializer))]
public class Evented<T> : ICSubject<T> {
    private T _value;
    
    public T Value {
        get => _value;
        set => onSet.OnNext(this._value = value);
    }
    
    private readonly Event<T> onSet;

    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    public Evented(T val) {
        _value = val;
        onSet = new Event<T>();
        onSet.OnNext(_value);
    }

    public static implicit operator T(Evented<T> evo) => evo._value;

    public IDisposable Subscribe(IObserver<T> observer) {
        observer.OnNext(_value);
        return onSet.Subscribe(observer);
    }

    public void OnNext(T value) => Value = value;

    public void OnError(Exception error) => onSet.OnError(error);

    public void OnCompleted() => onSet.OnCompleted();

    public void PublishIfNotSame(T value) {
        if (!Equals(value, _value))
            OnNext(value);
    }
}
}