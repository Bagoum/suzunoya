using System;
using System.Drawing;
using System.Numerics;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Suzunoya.ControlFlow;
using Suzunoya.Display;

namespace Suzunoya.Entities {
/// <summary>
/// Interface for entities that can be tinted.
/// </summary>
public interface ITinted : IEntity {
    /// <summary>
    /// Base tint of the entity.
    /// </summary>
    IdealOverride<FColor> Tint { get; }
    /// <summary>
    /// Base tint with disturbance effects applied.
    /// </summary>
    DisturbedProduct<FColor> ComputedTint { get; }
}
/// <summary>
/// Interface for renderable entities.
/// </summary>
public interface IRendered : ITransform, ITinted {
    /// <summary>
    /// The render group under which this object renders.
    /// <br/>A basic plugin setup would be to associate a RenderGroup with an engine camera and game-object layer,
    ///  then treat RenderLayer and SortingID as equivalent to engine "sorting layer" / "order in layer".
    /// </summary>
    Evented<RenderGroup?> RenderGroup { get; }
    /// <summary>
    /// The rendering layer of the entity.
    /// </summary>
    Evented<int> RenderLayer { get; }
    /// <summary>
    /// The sorting ID of the entity.
    /// </summary>
    DisturbedSum<int> SortingID { get; }
    /// <summary>
    /// Whether or not the render group should consider this sorting ID when automatically assigning sorting IDs.
    /// </summary>
    bool UseSortingIDAsReference { get; }
    /// <summary>
    /// Whether or not the entity is visible.
    /// </summary>
    DisturbedAnd Visible { get; }
    
    /// <summary>
    /// Set that this object should be rendered under the provided rendering group.
    /// <br/>If the object is already associated with a render group,
    /// then it should deattach from the existing render group.
    /// </summary>
    void AddToRenderGroup(RenderGroup group, int? sortingID = null);
}

/// <summary>
/// An entity with a transform that can be rendered.
/// </summary>
public class Rendered : Transform, IRendered {
    /// <inheritdoc />
    public Evented<RenderGroup?> RenderGroup { get; } = new(null);
    private IDisposable? renderGroupToken;
    /// <inheritdoc />
    public Evented<int> RenderLayer { get; }
    /// <inheritdoc />
    public DisturbedSum<int> SortingID { get; } = new(0);
    /// <inheritdoc />
    public bool UseSortingIDAsReference { get; set; } = true;
    /// <inheritdoc />
    public DisturbedAnd Visible { get; }
    /// <inheritdoc />
    public IdealOverride<FColor> Tint { get; } = new(FColor.White);
    /// <inheritdoc />
    public DisturbedProduct<FColor> ComputedTint { get; }
    
    /// <summary>
    /// The alpha value of the tint. GET returns the computed alpha, SET sets the base alpha.
    /// </summary>
    public float Alpha {
        get => ComputedTint.Value.a;
        set => Tint.Value = Tint.Value.WithA(value);
    }

    /// <summary>
    /// The default rendering layer for this class.
    /// <br/>NB: this should be "effectively static" as it is called in the constructor.
    /// </summary>
    protected virtual int DefaultRenderLayer => 0;

    /// <summary>
    /// Constructor for <see cref="Rendered"/>.
    /// </summary>
    public Rendered(Vector3? location = null, Vector3? eulerAnglesD = null, Vector3? scale = null, 
        bool visible = true, FColor? color = null) : base(location, eulerAnglesD, scale) {
        // ReSharper disable once VirtualMemberCallInConstructor
        RenderLayer = new(DefaultRenderLayer);
        Visible = new(visible);
        ComputedTint = new(Tint);
    }
    
    /// <inheritdoc />
    public void AddToRenderGroup(RenderGroup group, int? sortingID = null) {
        if (group.Container != Container)
            throw new Exception($"Cannot add rendered {this} to a render group in a different VNState");
        SortingID.OnNext(sortingID ?? group.NextSortingID());
        renderGroupToken?.Dispose();
        renderGroupToken = group.Add(this);
        RenderGroup.Value = group;
    }

    /// <inheritdoc />
    protected override void BindParent(ITransform? nParent) {
        base.BindParent(nParent);
        if (nParent is IRendered r)
            parentTokens.Add(Visible.AddDisturbance(r.Visible));
    }

    /// <inheritdoc />
    public override void Delete() {
        renderGroupToken?.Dispose();
        renderGroupToken = null;
        base.Delete();
    }
    
    /// <summary>
    /// Short for Visible.Value = false
    /// </summary>
    public void Hide() => Visible.Value = false;
    /// <summary>
    /// Short for Visible.Value = true
    /// </summary>
    public void Show() => Visible.Value = true;
    
    /// <inheritdoc />
    public override void ClearEvents() {
        base.ClearEvents();
        Tint.OnCompleted();
        ComputedTint.OnCompleted();
        RenderGroup.OnCompleted();
        RenderLayer.OnCompleted();
        SortingID.OnCompleted();
        Visible.OnCompleted();
    }
}

}