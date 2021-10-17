using System;
using System.Reactive;

namespace BagoumLib.Events {

/// <summary>
/// A wrapper that turns the return type of an observable into Unit.
/// </summary>
/// <typeparam name="T"></typeparam>
public class UnitEventProxy<T> : IObservable<Unit> {
    private readonly IObservable<T> ev;
    public UnitEventProxy(IObservable<T> ev) {
        this.ev = ev;
    }

    public IDisposable Subscribe(IObserver<Unit> observer) => 
        ev.Subscribe(new UnitObserverProxy<T>(observer));
}

/// <summary>
/// A wrapper that sends typed observations to a Unit-type observer.
/// </summary>
/// <typeparam name="T"></typeparam>
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