using System;
using JetBrains.Annotations;

namespace BagoumLib.Events {

public abstract class Observer<T> : IObserver<T> {
    private IDisposable? unsubscriber;
    
    public IDisposable Register(IObservable<T> provider) {
        unsubscriber?.Dispose();
        return unsubscriber = provider.Subscribe(this);
    }

    public abstract void OnNext(T value);

    public virtual void OnError(Exception error) {
        //TODO log?
        throw error;
    }

    public void OnCompleted() {
        unsubscriber?.Dispose();
    }
}
}