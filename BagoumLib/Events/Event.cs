using System;
using System.Collections.Generic;
using System.Reactive;
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
public interface IBSubject<T, U> : ISubject<T, U>, IBObservable<U> {
}

[PublicAPI]
public interface IBSubject<T> : IBSubject<T, T>, ISubject<T> {
    
}

[PublicAPI]
public class Event<T, U> : IBSubject<T, U> {
    private readonly DMCompactingArray<IObserver<U>> callbacks = new();
    public Maybe<U> LastPublished { get; private set; } = Maybe<U>.None;
    
    
    private readonly Func<T, U> mapper;
    public Event(Func<T, U> mapper) {
        this.mapper = mapper;
    }

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
    public virtual IDisposable Subscribe(IObserver<U> observer) => callbacks.Add(observer);

    
    /// <summary>
    /// Same as OnNext.
    /// </summary>
    /// <param name="value"></param>
    public virtual void OnNext(T value) {
        var mvalue = mapper(value);
        LastPublished = Maybe<U>.Of(mvalue);
        var ct = callbacks.Count;
        var nulled = 0;
        for (int ii = 0; ii < ct; ++ii) {
            if (callbacks.ExistsAt(ii))
                callbacks[ii].OnNext(mvalue);
            else
                ++nulled;
        }
        if (nulled > ct / 10)
            callbacks.Compact();
    }
}
[PublicAPI]
public class Event<T> : Event<T, T>, IBSubject<T> {
    public Event() : base(x => x) { }
}

/// <summary>
/// An event that records all its published values in a list.
/// </summary>
public class AccEvent<T> : Event<T> {
    private readonly List<T> published = new();
    public IReadOnlyList<T> Published => published;

    public override void OnNext(T value) {
        published.Add(value);
        base.OnNext(value);
    }

    /// <summary>
    /// Clear the accumulated values in Published.
    /// </summary>
    public void Clear() => published.Clear();
}
}