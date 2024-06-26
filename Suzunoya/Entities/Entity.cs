﻿using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Reflection;
using BagoumLib.Transitions;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;

namespace Suzunoya.Entities {
/// <summary>
/// The state of the entity in its lifetime.
/// </summary>
public enum EntityState : int {
    /// <summary>
    /// The entity is currently active.
    /// </summary>
    Active = 10,
    /// <summary>
    /// The entity is in the predeletion stage, when its events are cleared.
    /// </summary>
    Predeletion = 20,
    /// <summary>
    /// The entity is dead.
    /// </summary>
    Deleted = 30
}

//Not inheriting IEntity : ICoroutineRunner in order to provide more protection around
// always getting BoundedSuboperationToken when running eg. tweens.
/// <summary>
/// Interface representing top-level entities that can be added under <see cref="IVNState"/>.
/// </summary>
[PublicAPI]
public interface IEntity : IDisposable {
    /// <summary>
    /// Plugin libraries generally will try to construct mimics for all entities.
    /// However, some entities may not desire mimics (such as unsprited characters).
    /// </summary>
    bool MimicRequested { get; }
    
    /// <summary>
    /// Token representing the life of this entity. When cancelled, the entity is destroyed.
    /// </summary>
    ICancellee LifetimeToken { get; }
    
    /// <summary>
    /// Run a tweening function on the entity.
    /// </summary>
    VNOperation Tween(ITransition tweener);
    
    /// <summary>
    /// VN to which this entity is attached.
    /// </summary>
    IVNState Container { get; }
    
    internal void AddToVNState(IVNState container, IDisposable token);
    
    /// <summary>
    /// Update the entity.
    /// </summary>
    /// <param name="deltaTime">Time since last update</param>
    void Update(float deltaTime);
    /// <summary>
    /// Evented procced after Update is complete. Mimics may listen to this.
    /// </summary>
    Event<float> OnUpdate { get; }

    /// <summary>
    /// Clear events and other linked data on the object (but not the <see cref="EntityActive"/> event).
    /// <br/>Run within <see cref="Delete"/>, but can be manually run earlier.
    /// <br/>When calling <see cref="IVNState.DeleteAll"/>,
    ///  PreDelete is run on all entities before Delete is run on any.
    /// </summary>
    void PreDelete();
    
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
    ICObservable<EntityState> EntityActive { get; }
}

/// <summary>
/// A top-level entity that can be added under <see cref="VNState"/>.
/// </summary>
public abstract class Entity : IEntity {
    /// <inheritdoc/>
    public virtual bool MimicRequested => true;
    /// <inheritdoc/>
    public IVNState Container { get; protected set; } = null!;
    /// <summary>
    /// Disposable tokens bounded by the lifetime of this entity.
    /// </summary>
    protected readonly List<IDisposable> tokens = new();
    private readonly Cancellable lifetimeToken = new();
    /// <inheritdoc/>
    public ICancellee LifetimeToken => lifetimeToken;
    private readonly Coroutines cors = new();
    /// <inheritdoc/>
    public Event<float> OnUpdate { get; } = new();

    private Evented<EntityState> _EntityActive { get; } = new(EntityState.Active);
    /// <inheritdoc/>
    public ICObservable<EntityState> EntityActive => _EntityActive;

    void IEntity.AddToVNState(IVNState container, IDisposable token) {
        Container = container;
        tokens.Add(token);
    }

    /// <inheritdoc/>
    public virtual void Update(float deltaTime) {
        this.AssertActive();
        cors.Step();
        OnUpdate.OnNext(deltaTime);
    }

    /// <summary>
    /// Run a corountine on this entity.
    /// </summary>
    public void Run(IEnumerator ienum) {
        this.AssertActive();
        cors.Run(ienum, CoroutineOptions.ProcessThisFrame(Container.HasVNUpdatedThisFrame));
    }

    /// <summary>
    /// Add a token to <see cref="tokens"/>.
    /// </summary>
    protected void AddToken(IDisposable token) => tokens.Add(token);
    
    /// <summary>
    /// Subscribe to an event.
    /// </summary>
    public void Listen<T>(IObservable<T> obs, Action<T> listener) => AddToken(obs.Subscribe(listener));
    
    /// <inheritdoc/>
    public VNOperation Tween(ITransition tweener) => this.AssertActive().MakeVNOp(ct => 
        tweener.With(this.BindLifetime(ct), () => Container.dT)
            .Run(cors, CoroutineOptions.ProcessThisFrame(Container.HasVNUpdatedThisFrame)));

    
    /// <inheritdoc/>
    public void PreDelete() {
        if (_EntityActive.Value >= EntityState.Predeletion) return;
        ClearEvents();
        _EntityActive.OnNext(EntityState.Predeletion);
    }
    
    /// <summary>
    /// Run Event.OnCompleted for data events on this entity.
    /// </summary>
    public virtual void ClearEvents() {}

    /// <summary>
    /// SoftSkip all coroutines before deleting.
    /// </summary>
    public void SoftDelete() {
        if (_EntityActive.Value >= EntityState.Deleted) return;
        lifetimeToken.Cancel(ICancellee.SoftSkipLevel);
        cors.CloseRepeated();
        if (cors.Count > 0)
            throw new Exception($"Some entity coroutines were not closed in the softdelete process. " +
                                $"{this} has {cors.Count} remaining.");
        Delete();
    }
    
    /// <inheritdoc/>
    public virtual void Delete() {
        if (_EntityActive.Value >= EntityState.Deleted) return;
        PreDelete();
        lifetimeToken.Cancel(ICancellee.HardCancelLevel);
        tokens.DisposeAll();
        cors.CloseRepeated();
        if (cors.Count > 0)
            throw new Exception($"Some entity coroutines were not closed in the cull process. " +
                                $"{this} has {cors.Count} remaining.");
        _EntityActive.OnNext(EntityState.Deleted);
        _EntityActive.OnCompleted();
        if (!Container.Logs.CanSkipMessage(LogLevel.DEBUG1))
            Container.Logs.Log($"The entity {GetType().RName()} has been deleted.", LogLevel.DEBUG1);
    }
    
    private Entity AssertActive() {
        if (_EntityActive.Value > EntityState.Active)
            throw new DestroyedObjectException($"{this} has been destroyed. You cannot run any more operations on it.");
        return this;
    }
    
    /// <inheritdoc/>
    public override string ToString() => $"<{GetType().RName()}>";
}


}