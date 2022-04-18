using System;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// A class that receives observations of types T and publishes them as observations of type U.
/// </summary>
[PublicAPI]
public class MappedIBObservable<T, U> : IBObservable<U> {
    private readonly IBObservable<T> basis;
    private readonly Func<T, U> mapper;
    private readonly Event<U> ev;
    public Maybe<U> LastPublished => ev.LastPublished;

    public MappedIBObservable(IBObservable<T> basis, Func<T, U> mapper) {
        this.basis = basis;
        this.ev = new Event<U>();
        this.mapper = mapper;
        basis.Subscribe(v => ev.OnNext(mapper(v)));
    }

    public IDisposable Subscribe(IObserver<U> observer) => ev.Subscribe(observer);
}

/// <summary>
/// A class that receives observations of types T and publishes them as observations of type U.
/// <br/>Always has a "current" value, which is published to subscribers immediately.
/// </summary>
[PublicAPI]
public class MappedICObservable<T, U> : ICObservable<U> {
    private readonly ICObservable<T> basis;
    private readonly Func<T, U> mapper;
    private readonly Evented<U> ev;
    public U Value => ev.Value;

    public MappedICObservable(ICObservable<T> basis, Func<T, U> mapper) {
        this.basis = basis;
        this.ev = new Evented<U>(mapper(basis.Value));
        this.mapper = mapper;
        basis.Subscribe(v => ev.OnNext(mapper(v)));
    }

    public IDisposable Subscribe(IObserver<U> observer) => ev.Subscribe(observer);
}

}