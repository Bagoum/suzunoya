using System;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// A class that receives observations of types T and publishes them as observations of type U.
/// </summary>
[PublicAPI]
public class MappedObservable<T, U> : IBObservable<U> {
    private readonly IBObservable<T> basis;
    private readonly Func<T, U> mapper;
    private readonly Event<U> ev;
    public Maybe<U> LastPublished => ev.LastPublished;

    public MappedObservable(IBObservable<T> basis, Func<T, U> mapper) {
        this.basis = basis;
        this.ev = new Event<U>();
        this.mapper = mapper;
        basis.Subscribe(v => ev.OnNext(mapper(v)));
    }

    public IDisposable Subscribe(IObserver<U> observer) => ev.Subscribe(observer);

}
}