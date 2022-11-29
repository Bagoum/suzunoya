using System;
using BagoumLib.Functional;

namespace BagoumLib.Events {
/// <summary>
/// An observable with an unchanging value.
/// </summary>
public class ConstantObservable<T> : ICObservable<T> {
    /// <inheritdoc/>
    public T Value { get; }
    
    /// <summary>
    /// Create a new <see cref="ConstantObservable{T}"/>.
    /// </summary>
    /// <param name="value">Fixed value of the observable.</param>
    public ConstantObservable(T value) {
        Value = value;
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer) {
        observer.OnNext(Value);
        return NullDisposable.Default;
    }

}
}