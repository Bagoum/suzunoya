using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// An observable that tracks its last published value.
/// </summary>
[PublicAPI]
public interface IBObservable<out T> : IObservable<T> {
    /// <summary>
    /// Whether or not this observable has ever published a vlaue.
    /// </summary>
    bool HasValue { get; }
    
    /// <summary>
    /// The last published value of this observable.
    /// <br/>Undefined behavior if <see cref="HasValue"/> is false.
    /// </summary>
    T Value { get; }
}

/// <summary>
/// An observable that has a "current" value.
/// </summary>
[PublicAPI]
public interface ICObservable<out T> : IBObservable<T> {
    bool IBObservable<T>.HasValue => true;
}

/// <summary>
/// A subject that tracks its last published observable value.
/// </summary>
[PublicAPI]
public interface IBSubject<in T, out U> : ISubject<T, U>, IBObservable<U>;

/// <summary>
/// A subject that has a "current" value.
/// </summary>
[PublicAPI]
public interface ICSubject<in T, out U> : IBSubject<T, U>, ICObservable<U>;

/// <summary>
/// A subject that tracks its last published observable value.
/// </summary>
[PublicAPI]
public interface IBSubject<T> : ISubject<T>, IBSubject<T, T>;

/// <summary>
/// A subject that has a "current" value.
/// </summary>
[PublicAPI]
public interface ICSubject<T> : IBSubject<T>, ICSubject<T, T>;

/// <summary>
/// A subject that observes elements of type T, maps them to type U, and publishes mapped elements to observers.
/// </summary>
[PublicAPI]
public class Event<T, U> : IBSubject<T, U> {
    private readonly DMCompactingArray<IObserver<U>> callbacks = new();
    private readonly List<IDisposable> tokens = new();
    /// <inheritdoc/>
    public bool HasValue { get; private set; } = false;

    /// <inheritdoc/>
    public U Value { get; private set; } = default!;
    
    
    private readonly Func<T, U> mapper;
    /// <summary>
    /// Create an event with a function to map from intake to output values.
    /// </summary>
    public Event(Func<T, U> mapper) {
        this.mapper = mapper;
    }

    /// <inheritdoc/>
    public void OnError(Exception error) {
        var ct = callbacks.Count;
        for (int ii = 0; ii < ct; ++ii) {
            if (callbacks.ExistsAt(ii))
                callbacks[ii].OnError(error);
        }
        callbacks.Empty();
    }

    /// <inheritdoc/>
    public void OnCompleted() {
        var ct = callbacks.Count;
        for (int ii = 0; ii < ct; ++ii) {
            if (callbacks.ExistsAt(ii))
                callbacks[ii].OnCompleted();
        }
        callbacks.Empty();
        tokens.DisposeAll();
        tokens.Clear();
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<U> observer) => callbacks.Add(observer);

    /// <inheritdoc/>
    public virtual void OnNext(T value) {
        var mvalue = mapper(value);
        HasValue = true;
        Value = mvalue;
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


/// <summary>
/// A subject that observes elements of type T and publishes them to observers without modification.
/// </summary>
[PublicAPI]
public class Event<T> : Event<T, T>, IBSubject<T> {
    /// <summary>
    /// Create an event.
    /// </summary>
    public Event() : base(x => x) { }
}

/// <summary>
/// An event that records all its published values in a list.
/// </summary>
public class AccEvent<T> : Event<T> {
    private readonly List<T> published = new();
    /// <summary>
    /// All values published by this event.
    /// </summary>
    public IReadOnlyList<T> Published => published;

    /// <inheritdoc/>
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