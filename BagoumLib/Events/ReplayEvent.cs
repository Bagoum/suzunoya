using System;
using System.Reactive.Subjects;
using BagoumLib.DataStructures;

namespace BagoumLib.Events {
public class ReplayEvent<T> : Event<T> {
    private readonly DMCompactingArray<IObserver<T>> callbacks = new();
    public int History { get; }
    private readonly CircularList<T> buffer;

    public ReplayEvent(int history) {
        buffer = new(History = history);
    }

    public override IDisposable Subscribe(IObserver<T> observer) {
        var dsp = base.Subscribe(observer);
        for (int ii = buffer.Count; ii > 0; --ii) {
            observer.OnNext(buffer.SafeIndexFromBack(ii));
        }
        return dsp;
    }

    public override void OnNext(T value) {
        buffer.Add(value);
        base.OnNext(value);
    }
}
}