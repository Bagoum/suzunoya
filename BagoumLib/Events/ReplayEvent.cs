using System;
using System.Reactive;
using System.Reactive.Subjects;
using BagoumLib.DataStructures;

namespace BagoumLib.Events {
/// <summary>
/// An event that sends the <see cref="History"/> most recent published elements to any new subscribers.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ReplayEvent<T> : IBSubject<T> {
    /// <summary>
    /// Maximum number of recently published elements to send to new subscribers.
    /// </summary>
    public int History { get; }

    /// <inheritdoc/>
    public bool HasValue => ev.HasValue;
    /// <inheritdoc/>
    public T Value => ev.Value;
    
    private readonly CircularList<T> buffer;
    private readonly Event<T> ev = new();

    /// <summary>
    /// Create a new <see cref="ReplayEvent{T}"/>.
    /// </summary>
    /// <param name="history">Maximum number of recently published elements to send to new subscribers</param>
    public ReplayEvent(int history) {
        buffer = new(History = history);
    }

    private void ReplayFor(IObserver<T> observer) {
        for (int ii = buffer.Count; ii > 0; --ii) {
            observer.OnNext(buffer.SafeIndexFromBack(ii));
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer) {
        ReplayFor(observer);
        return ev.Subscribe(observer);
    }

    /// <inheritdoc/>
    public void OnNext(T value) {
        buffer.Add(value);
        ev.OnNext(value);
    }

    /// <inheritdoc/>
    public void OnCompleted() => ev.OnCompleted();
    
    /// <inheritdoc/>
    public void OnError(Exception error) => ev.OnError(error);
}
}