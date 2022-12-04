using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using BagoumLib.DataStructures;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;

namespace Suzunoya.Assertions {
/// <summary>
/// Base class for assertions that operate over Suzunoya entity objects
/// (specifically anything deriving <see cref="IRendered"/>).
/// </summary>
public abstract record EntityAssertion {
    /// <inheritdoc cref="IAssertion{T}.ID"/>
    public string? ID { get; init; }
    /// <inheritdoc cref="IAssertion{T}.Priority"/>
    public (int Phase, int Ordering) Priority { get; set; }
    /// <summary>
    /// The entity assertion for the <see cref="ITransform"/>
    ///  that is a parent for this assertion's bound object.
    /// See <see cref="EntityAssertion{T}.TakeParent"/>
    /// </summary>
    protected EntityAssertion? Parent;
    /// <summary>
    /// Assertions for all the <see cref="ITransform"/>s that
    ///  are children of this assertion's bound object.
    /// See <see cref="EntityAssertion{T}.WithChildren"/>
    /// </summary>
    public List<IAssertion> Children { get; } = new();
    /// <summary>
    /// Bound to <see cref="ITransform.Location"/>
    /// </summary>
    public Vector3 Location { get; init; }
    /// <summary>
    /// Bound to <see cref="ITransform.EulerAnglesD"/>
    /// </summary>
    public Vector3 EulerAnglesD { get; init; }
    /// <summary>
    /// Bound to <see cref="ITransform.Scale"/>
    /// </summary>
    public Vector3 Scale { get; init; } = Vector3.One;
    /// <summary>
    /// Bound to <see cref="IRendered.Tint"/>
    /// </summary>
    public FColor Tint { get; init; } = FColor.White;

    /// <summary>
    /// The renderable object produced by this assertion.
    /// </summary>
    public abstract IRendered BoundGeneric { get; }
    
    /// <summary>
    /// Set the <see cref="Parent"/> of this assertion.
    /// </summary>
    public void TakeParent(EntityAssertion parent) {
        Parent = parent;
        if (parent.Priority.Phase > Priority.Phase)
            Priority = (parent.Priority.Phase, Priority.Ordering);
        if (parent.Priority.Ordering >= Priority.Ordering)
            Priority = (Priority.Phase, parent.Priority.Ordering + 1);
    }
}

/// <summary>
/// Assertions that operate over Suzunoya entity objects
/// (specifically anything deriving <see cref="IRendered"/>).
/// </summary>
/// <typeparam name="C">Type of entity</typeparam>
public record EntityAssertion<C> : EntityAssertion, IChildLinkedAssertion, IAssertion<EntityAssertion<C>> where C : IRendered, new() {
    /// <summary>
    /// VNState within which this entity is contained.
    /// </summary>
    public IVNState vn { get; init; }
    /// <summary>
    /// Extra bindings to apply to actualized objects.
    /// </summary>
    public Action<C>? ExtraBind { get; init; }
    
    /// <summary>
    /// Callback to invoke when an actualized object is first created (not during inheritance).
    /// </summary>
    public Action<C>? OnActualize { get; init; }
    
    /// <summary>
    /// Whether or not this object should perform an entry animation (such as a fade-in)
    ///  after its object is created during gameplay.
    /// </summary>
    public bool DynamicEntryAllowed { get; init; } = true;

    /// <summary>
    /// Default entry animation.
    /// </summary>
    protected virtual Task DefaultDynamicEntryHandler(C c) {
        c.Tint.Value = c.Tint.Value.WithA(0);
        return c.FadeTo(1, 1).Task;
    }
    /// <summary>
    /// Entry animation. Defaults to <see cref="DefaultDynamicEntryHandler"/>. Only runs if <see cref="DynamicEntryAllowed"/> is set to true.
    /// </summary>
    public Func<C, Task>? DynamicEntryHandler { get; init; }

    /// <summary>
    /// Whether or not this object should perform an exit animation (such as a fade-out)
    ///  before its object is destroyed during gameplay.
    /// </summary>
    public bool DynamicExitAllowed { get; set; } = true;
    
    /// <summary>
    /// Default exit animation.
    /// </summary>
    protected virtual Task DefaultDynamicExitHandler(C c) {
        return c.FadeTo(0, 1).Task;
    }
    /// <summary>
    /// Exit animation. Defaults to <see cref="DefaultDynamicExitHandler"/>. Only runs if <see cref="DynamicExitAllowed"/> is set to true.
    /// </summary>
    public Func<C, Task>? DynamicExitHandler { get; set; }

    /// <inheritdoc cref="BoundGeneric"/>
    protected C Bound { get; set; } = default!;
    
    /// <inheritdoc/>
    public override IRendered BoundGeneric => Bound;

    /// <summary>
    /// Enable ad set an exit animation.
    /// </summary>
    public void HandleDynamicExit(Func<C, Task> handler) {
        DynamicExitAllowed = true;
        DynamicExitHandler = handler;
    }

    /// <summary>
    /// Create an entity assertion for the given entity type.
    /// </summary>
    public EntityAssertion(IVNState vn, string? id = null) {
        this.vn = vn;
        this.ID = id;
        this.DynamicEntryHandler = DefaultDynamicEntryHandler;
        this.DynamicExitHandler = DefaultDynamicExitHandler;
    }

    /// <summary>
    /// Bind the ent's fields to the values in this assertion. Run before the ent is added to the VNState.
    /// </summary>
    protected virtual void Bind(C ent) {
        Bound = ent;
        ent.Location.SetIdeal(Location);
        ent.EulerAnglesD.SetIdeal(EulerAnglesD);
        ent.Scale.SetIdeal(Scale);
        ent.Tint.SetIdeal(Tint);
        ExtraBind?.Invoke(ent);
    }

    /// <summary>
    /// Bind the ent's fields to the values in this assertion. Run after the object is added to the VNState.
    /// </summary>
    protected virtual void LateBind(C ent) {
        if (Parent is { BoundGeneric: null })
            throw new Exception("Child constructed before parent!");
        ent.Parent = Parent?.BoundGeneric;
    }

    /// <inheritdoc />
    public Task ActualizeOnNewState() {
        var obj = new C();
        Bind(obj);
        vn.Add(obj);
        LateBind(obj);
        OnActualize?.Invoke(Bound);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ActualizeOnNoPreceding() {
        await ActualizeOnNewState();
        if (DynamicEntryAllowed && DynamicEntryHandler != null)
            await DynamicEntryHandler(Bound);
    }

    /// <inheritdoc />
    public Task DeactualizeOnEndState() {
        Bound.Delete();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DeactualizeOnNoSucceeding() {
        if (DynamicExitAllowed && DynamicExitHandler != null)
            await DynamicExitHandler(Bound);
        Bound.Delete();
    }

    /// <summary>
    /// Add children to this assertion, calling <see cref="EntityAssertion.TakeParent"/> on each of them.
    /// </summary>
    public EntityAssertion<C> WithChildren(params EntityAssertion?[] children) {
        foreach (var c in children) {
            if (c != null) {
                c.TakeParent(this);
                Children.Add((c as IAssertion) ?? throw new Exception());
            }
        }
        return this;
    }

    Task IAssertion.Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
    
    /// <inheritdoc />
    public Task _Inherit(EntityAssertion<C> prev) {
        Bind(prev.Bound);
        return Task.CompletedTask;
    }
}
}