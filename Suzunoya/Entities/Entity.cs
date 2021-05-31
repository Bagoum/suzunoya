using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Reflection;
using BagoumLib.Tweening;
using Suzunoya.ControlFlow;

namespace Suzunoya.Entities {
//Not inheriting IEntity : ICoroutineRunner in order to provide more protection around
// always getting BoundedSuboperationToken when running eg. tweens.
public interface IEntity {
    ICancellee LifetimeToken { get; }
    VNOperation Tween(ITweener tweener);
    IVNState Container { get; }
    void AddToVNState(IVNState container);
    void Update(float deltaTime);
    void Delete();
    /// <summary>
    /// When this is set to false, the object is destroyed and no further operations can be run.
    /// </summary>
    Evented<bool> EntityActive { get; }
}

public abstract class Entity : IEntity {
    public IVNState Container { get; protected set; } = null!;
    protected readonly List<IDisposable> tokens = new();
    private readonly Cancellable lifetimeToken = new();
    public ICancellee LifetimeToken => lifetimeToken;
    private readonly Coroutines cors = new();

    public Evented<bool> EntityActive { get; } = new(true);

    public virtual void AddToVNState(IVNState container) {
        Container = container;
        tokens.Add(container.AddEntity(this));
    }

    public virtual void Update(float deltaTime) {
        this.AssertActive();
        cors.Step();
    }

    public void Run(IEnumerator ienum, CoroutineOptions? opts = null) {
        this.AssertActive();
        cors.Run(ienum, opts);
    }

    protected void AddToken(IDisposable token) => tokens.Add(token);
    public void Listen<T>(IObservable<T> obs, Action<T> listener) => AddToken(obs.Subscribe(listener));

    public VNOperation Tween(ITweener tweener) => this.AssertActive().MakeVNOp(ct => 
        tweener.With(this.BindLifetime(ct), () => Container.dT).Run(cors));

    public virtual void Delete() {
        this.AssertActive();
        lifetimeToken.Cancel(CancelHelpers.HardCancelLevel);
        foreach (var t in tokens)
            t.Dispose();
        tokens.Clear();
        cors.Close();
        if (cors.Count > 0)
            throw new Exception($"Some entity coroutines were not closed in the cull process. " +
                                $"{this} has {cors.Count} remaining.");
        EntityActive.OnNext(false);
    }
    
    private Entity AssertActive() {
        if (EntityActive.Value == false)
            throw new DestroyedObjectException($"The entity {GetType().RName()} has been destroyed. " +
                                               "You cannot run any more operations on it.");
        return this;
    }
}


}