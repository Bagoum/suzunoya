using System;
using System.Collections.Generic;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {

/// <summary>
/// An event proxy is a wrapper around an observable X
///  that allows subscription to the events defined on the current value of X.
/// <br/>When X changes, the subscriptions are rolled forward to the events on the new value of X.
/// <br/>Note that the old subscriptions are not deleted, so if an old value of X hangs around, it
///  can still trigger any subscriptions made to it.
/// </summary>
/// <typeparam name="T">Type of X.</typeparam>
[PublicAPI]
public class EventProxy<T> {
    private readonly IObservable<T> sourcer;
    private readonly DMCompactingArray<IEventProxySubscription> resubscribers = new();
    private Maybe<T> lastRead;
    public EventProxy(IObservable<T> sourcer) {
        this.sourcer = sourcer;
        sourcer.Subscribe(obj => {
            lastRead = obj;
            ResubscribeAll(obj);
        });
    }

    /// <summary>
    /// Subscribe to an observable defined by evGetter on the value of the sourcer.
    /// When the sourcer changes, this observable will also report values from the new value of the sourcer.
    /// </summary>
    public IDisposable Subscribe<E>(Func<T, IObservable<E>> evGetter, Action<E> listener) {
        var subscription = new IndirectEventSubscription<E>(evGetter, listener);
        if (lastRead.Try(out var obj))
            subscription.GenerateSubscriptions(obj);
        //The token should do two things: remove the subscription from the resubscribers list,
        // and also detach any existing event listeners within the subscription (EventProxySubscription.Dispose).
        return new JointDisposable(null, subscription, resubscribers.Add(subscription));
    }

    /// <summary>
    /// Create an observable that reports the value of an observable defined by evGetter
    ///  on the value of the sourcer. When the sourcer changes, this observable will
    ///  also report values from the new value of the sourcer.
    /// </summary>
    public IObservable<E> ProxyEvent<E>(Func<T, IObservable<E>> evGetter) {
        var partialEv = new ProxiedEvent<E>(evGetter);
        if (lastRead.Try(out var obj))
            partialEv.GenerateSubscriptions(obj);
        //This token does not need to be tracked, as proxied events can be considered as simple redirection
        // pointers with no significant behavior of their own (and therefore they never need to be deleted).
        _ = resubscribers.Add(partialEv);
        return partialEv;
    }

    private void ResubscribeAll(T obj) {
        resubscribers.Compact();
        for (int ii = 0; ii < resubscribers.Count; ++ii)
            resubscribers[ii].GenerateSubscriptions(obj);
    }

    private interface IEventProxySubscription {
        void GenerateSubscriptions(T target);
    }

    private class ProxiedEvent<E> : IObservable<E>, IEventProxySubscription {
        private readonly Func<T, IObservable<E>> evGetter;
        private readonly Event<E> ev = new();
        
        public ProxiedEvent(Func<T, IObservable<E>> evGetter) {
            this.evGetter = evGetter;
        }

        public IDisposable Subscribe(IObserver<E> observer) {
            return ev.Subscribe(observer);
        }

        public void GenerateSubscriptions(T target) {
            _ = evGetter(target).Subscribe(ev.OnNext);
        }
    }
    private class IndirectEventSubscription<E> : IEventProxySubscription, IDisposable {
        private readonly Func<T, IObservable<E>> getter;
        private readonly Action<E> listener;
        private readonly List<IDisposable> tokens = new();
        public IndirectEventSubscription(Func<T, IObservable<E>> getter, Action<E> listener) {
            this.getter = getter;
            this.listener = listener;
        }

        public void GenerateSubscriptions(T target) {
            tokens.Add(getter(target).Subscribe(listener));
        }

        public void Dispose() {
            foreach (var t in tokens)
                t.Dispose();
            tokens.Clear();
        }
    }
}
}