using System;
using System.Reactive.Subjects;
using BagoumLib.DataStructures;

namespace BagoumLib.Events {
/// <summary>
/// An event that sends the <see cref="History"/> most recent published elements to any new subscribers.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ReplayEvent<T> : Event<T> {
    private readonly DMCompactingArray<IObserver<T>> callbacks = new();
    /// <summary>
    /// Maximum number of recently published elements to send to new subscribers.
    /// </summary>
    public int History { get; }
    private readonly CircularList<T> buffer;

    /// <summary>
    /// Create a new <see cref="ReplayEvent{T}"/>.
    /// </summary>
    /// <param name="history">Maximum number of recently published elements to send to new subscribers</param>
    public ReplayEvent(int history) {
        buffer = new(History = history);
    }

    /// <inheritdoc/>
    public override IDisposable Subscribe(IObserver<T> observer) {
        var dsp = base.Subscribe(observer);
        for (int ii = buffer.Count; ii > 0; --ii) {
            observer.OnNext(buffer.SafeIndexFromBack(ii));
        }
        return dsp;
    }

    /// <inheritdoc/>
    public override void OnNext(T value) {
        buffer.Add(value);
        base.OnNext(value);
    }
}
}