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

public class MappedObserver<T, U> : IObserver<T> {
    private readonly IObserver<U> observer;
    private readonly Func<T, U> mapper;

    public MappedObserver(IObserver<U> observer, Func<T, U> mapper) {
        this.observer = observer;
        this.mapper = mapper;
    }

    public void OnNext(T value) => observer.OnNext(mapper(value));

    public void OnError(Exception error) => observer.OnError(error);

    public void OnCompleted() => observer.OnCompleted();
}

}