using System;
using System.Reactive;

namespace BagoumLib.Events {

public abstract class UnitEventProxy : IObservable<Unit> {
    public abstract IDisposable Subscribe(IObserver<Unit> observer);
}

public class UnitEventProxy<T> : UnitEventProxy {
    private readonly IObservable<T> ev;
    public UnitEventProxy(IObservable<T> ev) {
        this.ev = ev;
    }

    public override IDisposable Subscribe(IObserver<Unit> observer) => 
        ev.Subscribe(new UnitObserverProxy<T>(observer));
}

public class UnitObserverProxy<T> : IObserver<T> {
    private readonly IObserver<Unit> obs;
    public UnitObserverProxy(IObserver<Unit> obs) {
        this.obs = obs;
    }

    public void OnNext(T value) => obs.OnNext(Unit.Default);

    public void OnError(Exception error) => obs.OnError(error);

    public void OnCompleted() => obs.OnCompleted();
}
}