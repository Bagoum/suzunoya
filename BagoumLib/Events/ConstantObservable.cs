using System;
using BagoumLib.Functional;

namespace BagoumLib.Events {
public class ConstantObservable<T> : ICObservable<T> {
    public T Value { get; }
    
    public ConstantObservable(T value) {
        Value = value;
    }

    public IDisposable Subscribe(IObserver<T> observer) {
        observer.OnNext(Value);
        return NullDisposable.Default;
    }

}
}