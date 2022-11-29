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
using BagoumLib.Transitions;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;

namespace Suzunoya.Entities {
//Not inheriting IEntity : ICoroutineRunner in order to provide more protection around
// always getting BoundedSuboperationToken when running eg. tweens.
[PublicAPI]
public interface IEntity : IDisposable {
    /// <summary>
    /// Plugin libraries generally will try to construct mimics for all entities.
    /// However, some entities may not desire mimics (such as unsprited characters).
    /// </summary>
    bool MimicRequested { get; }
    ICancellee LifetimeToken { get; }
    VNOperation Tween(ITransition tweener);
    IVNState Container { get; }
    void AddToVNState(IVNState container, IDisposable token);
    void Update(float deltaTime);
    /// <summary>
    /// Called after Update is complete. Mimics may listen to this.
    /// </summary>
    Event<float> OnUpdate { get; }
    
    /// <summary>
    /// Destroy the object and sets EntityActive to false.
    /// Same as <see cref="IDisposable.Dispose"/>.
    /// </summary>
    /// <exception cref="Exception">Thrown if any running coroutines cannot be closed.</exception>
    void Delete();

    void IDisposable.Dispose() => Delete();
    
    /// <summary>
    /// When this is set to false, the object is destroyed and no further operations can be run.
    /// <br/>Do not modify this externally. To destroy the object, run Delete().
    /// </summary>
    ICObservable<bool> EntityActive { get; }
}

public abstract class Entity : IEntity {
    public virtual bool MimicRequested => true;
    public IVNState Container { get; protected set; } = null!;
    protected readonly List<IDisposable> tokens = new();
    private readonly Cancellable lifetimeToken = new();
    public ICancellee LifetimeToken => lifetimeToken;
    private readonly Coroutines cors = new();
    public Event<float> OnUpdate { get; } = new();

    private Evented<bool> _EntityActive { get; } = new(true);
    public ICObservable<bool> EntityActive => _EntityActive;

    public virtual void AddToVNState(IVNState container, IDisposable token) {
        Container = container;
        tokens.Add(token);
    }

    public virtual void Update(float deltaTime) {
        this.AssertActive();
        cors.Step();
        OnUpdate.OnNext(deltaTime);
    }


    public void Run(IEnumerator ienum, CoroutineOptions? opts = null) {
        this.AssertActive();
        cors.Run(ienum, opts);
    }

    protected void AddToken(IDisposable token) => tokens.Add(token);
    public void Listen<T>(IObservable<T> obs, Action<T> listener) => AddToken(obs.Subscribe(listener));

    public VNOperation Tween(ITransition tweener) => this.AssertActive().MakeVNOp(ct => 
        tweener.With(this.BindLifetime(ct), () => Container.dT).Run(cors));

    /// <summary>
    /// SoftSkip all coroutines before deleting.
    /// </summary>
    public void SoftDelete() {
        lifetimeToken.Cancel(ICancellee.SoftSkipLevel);
        cors.CloseRepeated();
        if (cors.Count > 0)
            throw new Exception($"Some entity coroutines were not closed in the softdelete process. " +
                                $"{this} has {cors.Count} remaining.");
        Delete();
    }
    public virtual void Delete() {
        if (_EntityActive.Value == false) return;
        lifetimeToken.Cancel(ICancellee.HardCancelLevel);
        foreach (var t in tokens)
            t.Dispose();
        tokens.Clear();
        cors.CloseRepeated();
        if (cors.Count > 0)
            throw new Exception($"Some entity coroutines were not closed in the cull process. " +
                                $"{this} has {cors.Count} remaining.");
        _EntityActive.OnNext(false);
        Container.Logs.OnNext($"The entity {GetType().RName()} has been deleted.");
    }
    
    private Entity AssertActive() {
        if (_EntityActive.Value == false)
            throw new DestroyedObjectException($"{this} has been destroyed. You cannot run any more operations on it.");
        return this;
    }
    
    public override string ToString() => $"<{GetType().RName()}>";
}


}