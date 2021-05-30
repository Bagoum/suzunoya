using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// A ComputedEvented is a representation of a function that depends on multiple input values.
/// When any of those input values are updated, the value of this will be updated with an event.
/// </summary>
[PublicAPI]
public class ComputedEvented<T> : IBSubject<T> {
    private T _value;
    private readonly Func<T> getter;
    
    public T Value {
        get => _value;
        set => onSet.OnNext(this._value = value);
    }
    public Maybe<T> LastPublished => Maybe<T>.Of(_value);
    
    private readonly Event<T> onSet;
    private readonly List<IDisposable> tokens = new List<IDisposable>();

    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    public ComputedEvented(Func<T> val, params IObservable<Unit>[] triggers) {
        _value = (this.getter = val)();
        onSet = new Event<T>();
        foreach (var t in triggers)
            tokens.Add(t.Subscribe(_ => Recompute()));
        Recompute();
    }

    public static implicit operator T(ComputedEvented<T> evo) => evo._value;

    public IDisposable Subscribe(IObserver<T> observer) {
        observer.OnNext(_value);
        return (onSet ?? throw new Exception("Computed event not initialized")).Subscribe(observer);
    }

    public void Recompute() {
        var nv = getter();
        if (!Equals(nv, _value))
            OnNext(nv);
    }

    public void OnNext(T value) => Value = value;

    public void OnError(Exception error) => onSet.OnError(error);

    public void OnCompleted() => onSet.OnCompleted();
}
}