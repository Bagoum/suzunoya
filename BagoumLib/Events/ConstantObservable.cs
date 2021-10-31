using System;
using BagoumLib.Functional;

namespace BagoumLib.Events {
public class ConstantObservable<T> : IBObservable<T> {
    public T Value { get; }
    public Maybe<T> LastPublished { get; }
    
    public ConstantObservable(T value) {
        Value = value;
        LastPublished = Value;
    }

    public IDisposable Subscribe(IObserver<T> observer) {
        observer.OnNext(Value);
        return NullDisposable.Default;
    }

}
}