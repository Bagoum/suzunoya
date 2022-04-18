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
public abstract record EntityAssertion {
    public string? ID { get; init; }
    public (int Phase, int Ordering) Priority { get; set; }
    protected EntityAssertion? Parent;
    public List<IAssertion> Children { get; } = new();
    public Vector3 Location { get; init; }
    public Vector3 EulerAnglesD { get; init; }
    public Vector3 Scale { get; init; } = Vector3.One;
    public FColor Tint { get; init; } = FColor.White;

    public abstract IRendered BoundGeneric { get; }
    
    public void TakeParent(EntityAssertion parent) {
        Parent = parent;
        if (parent.Priority.Phase > Priority.Phase)
            Priority = (parent.Priority.Phase, Priority.Ordering);
        if (parent.Priority.Ordering >= Priority.Ordering)
            Priority = (Priority.Phase, parent.Priority.Ordering + 1);
    }
}
public record EntityAssertion<C> : EntityAssertion, IChildLinkedAssertion, IAssertion<EntityAssertion<C>> where C : IRendered, new() {
    public IVNState vn { get; init; }
    /// <summary>
    /// Extra bindings to apply to actualized objects.
    /// </summary>
    public Action<C>? ExtraBind { get; init; }
    
    /// <summary>
    /// Callback to invoke when an actualized object is first create (not during inheritance).
    /// </summary>
    public Action<C>? OnActualize { get; init; }
    
    public bool DynamicEntryAllowed { get; init; } = true;

    protected virtual Task DefaultDynamicEntryHandler(C c) {
        c.Tint.Value = c.Tint.Value.WithA(0);
        return c.FadeTo(1, 1).Task;
    }
    public Func<C, Task>? DynamicEntryHandler { get; init; }

    public bool DynamicExitAllowed { get; set; } = true;
    
    protected virtual Task DefaultDynamicExitHandler(C c) {
        return c.FadeTo(0, 1).Task;
    }
    public Func<C, Task>? DynamicExitHandler { get; set; }

    protected C Bound { get; set; } = default!;
    public override IRendered BoundGeneric => Bound;

    public void HandleDynamicExit(Func<C, Task> handler) {
        DynamicExitAllowed = true;
        DynamicExitHandler = handler;
    }

    public EntityAssertion(IVNState vn, string? id = null) {
        this.vn = vn;
        this.ID = id;
        this.DynamicEntryHandler = DefaultDynamicEntryHandler;
        this.DynamicExitHandler = DefaultDynamicExitHandler;
    }

    protected virtual void Bind(C ent) {
        Bound = ent;
        ent.Location.SetIdeal(Location);
        ent.EulerAnglesD.SetIdeal(EulerAnglesD);
        ent.Scale.SetIdeal(Scale);
        ent.Tint.SetIdeal(Tint);
        ExtraBind?.Invoke(ent);
    }

    /// <summary>
    /// Bindings run after the object is added to the VNState.
    /// </summary>
    protected virtual void LateBind(C ent) {
        if (Parent is { BoundGeneric: null })
            throw new Exception("Child constructed before parent!");
        ent.Parent = Parent?.BoundGeneric;
    }

    public Task ActualizeOnNewState() {
        var obj = new C();
        Bind(obj);
        vn.Add(obj);
        LateBind(obj);
        OnActualize?.Invoke(Bound);
        return Task.CompletedTask;
    }

    public async Task ActualizeOnNoPreceding() {
        await ActualizeOnNewState();
        if (DynamicEntryAllowed && DynamicEntryHandler != null)
            await DynamicEntryHandler(Bound);
    }

    public Task DeactualizeOnEndState() {
        Bound.Delete();
        return Task.CompletedTask;
    }

    public async Task DeactualizeOnNoSucceeding() {
        if (DynamicExitAllowed && DynamicExitHandler != null)
            await DynamicExitHandler(Bound);
        Bound.Delete();
    }

    public EntityAssertion<C> WithChildren(params EntityAssertion[] children) {
        foreach (var c in children) {
            c.TakeParent(this);
            Children.Add((c as IAssertion) ?? throw new Exception());
        }
        return this;
    }

    Task IAssertion.Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
    public Task _Inherit(EntityAssertion<C> prev) {
        Bind(prev.Bound);
        return Task.CompletedTask;
    }
}
}