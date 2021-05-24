using System;
using System.Reactive.Subjects;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {
[PublicAPI]
public interface IBObservable<T> : IObservable<T> {
    Maybe<T> LastPublished { get; }
}
[PublicAPI]
public interface IBSubject<T> : ISubject<T>, IBObservable<T> {
    void Publish(T value);
}

[PublicAPI]
public class Event<T> : IBSubject<T> {
    private readonly DMCompactingArray<IObserver<T>> callbacks = new DMCompactingArray<IObserver<T>>();
    public Maybe<T> LastPublished { get; private set; } = Maybe<T>.None;


    public void OnNext(T value) => Publish(value);

    public void OnError(Exception error) {
        var ct = callbacks.Count;
        for (int ii = 0; ii < ct; ++ii) {
            if (callbacks.ExistsAt(ii))
                callbacks[ii].OnError(error);
        }
    }

    public virtual void OnCompleted() {
        var ct = callbacks.Count;
        for (int ii = 0; ii < ct; ++ii) {
            if (callbacks.ExistsAt(ii))
                callbacks[ii].OnCompleted();
        }
        callbacks.Empty();
    }

    /// <summary>
    /// Do not call this directly. Use Observer.Register instead.
    /// </summary>
    public IDisposable Subscribe(IObserver<T> observer) => callbacks.Add(observer);

    
    /// <summary>
    /// Same as OnNext.
    /// </summary>
    /// <param name="value"></param>
    public void Publish(T value) {
        LastPublished = Maybe<T>.Of(value);
        var ct = callbacks.Count;
        var nulled = 0;
        for (int ii = 0; ii < ct; ++ii) {
            if (callbacks.ExistsAt(ii))
                callbacks[ii].OnNext(value);
            else
                ++nulled;
        }
        if (nulled > ct / 10)
            callbacks.Compact();
    }
}

/// <summary>
/// A hub that cannot be closed via OnCompleted.
/// </summary>
public class PersistentEvent<T> : Event<T> {
    public override void OnCompleted() {
        throw new Exception("Persistent hubs cannot be closed");
    }
}
}