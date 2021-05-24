using System;
using System.Reactive.Subjects;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// Evented is a wrapper around a value that publishes an event whenever it is set.
/// New subscribers are sent the existing value as well as any new values.
/// <br/>(This is effectively the same as BehaviorSubject.)
/// </summary>
[PublicAPI]
public class Evented<T> : IBSubject<T> {
    private T _value;
    
    public T Value {
        get => _value;
        set => onSet.Publish(this._value = value);
    }
    public Maybe<T> LastPublished => Maybe<T>.Of(_value);
    
    private readonly Event<T> onSet;

    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    public Evented(T val) {
        _value = val;
        onSet = new Event<T>();
        onSet.Publish(_value);
    }

    public static implicit operator T(Evented<T> evo) => evo._value;

    public IDisposable Subscribe(IObserver<T> observer) {
        observer.OnNext(_value);
        return onSet.Subscribe(observer);
    }

    public void OnNext(T value) => Value = value;

    public void OnError(Exception error) => onSet.OnError(error);

    public void OnCompleted() => onSet.OnCompleted();
    
    public void Publish(T value) => OnNext(value);
}
}