using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Sorting;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;

namespace Suzunoya.Display {
/// <summary>
/// A group of objects rendered by the same camera to a unique texture.
/// The Location/EulerAnglesD fields should be considered as referring to the transform
///  of the render group's associated camera.
/// The Scale field should be considered as referring to axis-specific zoom multipliers. As this requires nontrivial
///  shader implementation in some engines, it is separated from Zoom, which is a single field.
/// A render group is also a rendered object and this has similar fields to IRendered.
/// </summary>
public class RenderGroup : Transform, ITinted {
    public static int DefaultSortingIDStep { get; set; } = 10;
    public const string DEFAULT_KEY = "$default";
    public string Key { get; }

    /// <summary>
    /// A render group may render to another render group. Roughly equivalent to IRendered.RenderGroup,
    ///  but it is a valid case for this to be null (in which case it renders to screen).
    /// </summary>
    public Evented<RenderGroup?> NestedRenderGroup { get; } = new(null);

    /// <summary>
    /// Equivalent to IRendered.RenderLayer.
    /// </summary>
    public Evented<int> RenderLayer { get; } = new(0);
    
    /// <summary>
    /// Equivalent to IRendered.SortingID.
    /// </summary>
    public Evented<int> Priority { get; }
    /// <summary>
    /// Whether or not the render group is visible.
    /// </summary>
    public Evented<bool> Visible { get; }
    /// <inheritdoc/>
    public IdealOverride<FColor> Tint { get; } = new(FColor.White);
    /// <inheritdoc/>
    public DisturbedProduct<FColor> ComputedTint { get; }
    
    public float Alpha {
        get => ComputedTint.Value.a;
        set => Tint.Value = Tint.Value.WithA(value);
    }

    /// <summary>
    /// The camera zoom applied to the render group.
    /// </summary>
    public Evented<float> Zoom { get; } = new(1);
    
    /// <summary>
    /// The target that should be zoomed in on or away from when <see cref="Zoom"/> != 1.
    /// </summary>
    public Evented<Vector3> ZoomTarget { get; } = new(Vector3.Zero);
    
    /// <summary>
    /// An offset that should be added to the camera position to handle simulating <see cref="ZoomTarget"/> on
    ///  cameras that only support zooming upon their center.
    /// </summary>
    public LazyEvented<Vector3> ZoomTransformOffset { get; }
    
    /// <summary>
    /// The elements rendered within this rendering group.
    /// </summary>
    public DMCompactingArray<IRendered> Contents { get; } = new();
    /// <summary>
    /// An event that is fired whenever a new element is added to this rendering group.
    /// </summary>
    public Event<IRendered> RendererAdded { get; } = new();

    public RenderGroup(IVNState container, string key = DEFAULT_KEY, int priority = 0, bool visible = false) {
        ZoomTransformOffset = new(() => (Zoom - 1) / Zoom * (ZoomTarget.Value - ComputedLocation), 
            Zoom.Erase(), ZoomTarget.Erase());
        Container = container;
        Key = key;
        Priority = new(priority);
        Visible = new(visible);
        ComputedTint = new(Tint);
        tokens.Add(Container._AddRenderGroup(this));
    }

    /// <summary>
    /// DO NOT CALL THIS. VNState should be provided in the constructor of <see cref="RenderGroup"/>.
    /// </summary>
    void IEntity.AddToVNState(IVNState container, IDisposable _) =>
        throw new Exception("Do not call RenderGroup.AddToVNState. Provide the VNState in the constructor.");

    /// <summary>
    /// Add a new element to this rendering group.
    /// </summary>
    public IDisposable Add(IRendered rnd) {
        var dsp = Contents.Add(rnd);
        RendererAdded.OnNext(rnd);
        return dsp;
    }

    private static readonly IComparer<DeletionMarker<IRendered>> SortingIDCompare =
        Comparer<DeletionMarker<IRendered>>.Create((a, b) => a.Value.SortingID.Value.CompareTo(b.Value.SortingID.Value));
    
    /// <summary>
    /// Sort <see cref="Contents"/> by sorting ID.
    /// </summary>
    private void SortContents() {
        Contents.Sort(SortingIDCompare);
    }

    public int NextSortingID() {
        var m = -1 * DefaultSortingIDStep;
        for (int ii = 0; ii < Contents.Count; ++ii) {
            if (Contents.GetIfExistsAt(ii, out var r) && r.UseSortingIDAsReference)
                m = Math.Max(m, r.SortingID.BaseValue);
        }
        return m + DefaultSortingIDStep;
    }

    private Cancellable? transitionToken;

    public ICancellee GetTransitionToken() {
        transitionToken?.Cancel(ICancellee.HardCancelLevel);
        transitionToken = new Cancellable();
        return new JointCancellee(LifetimeToken, transitionToken);
    }

    /// <inheritdoc/>
    public override void Delete() {
        for (int ii = 0; ii < Contents.Count; ++ii) {
            if (Contents.ExistsAt(ii))
                Contents[ii].Delete();
        }
        base.Delete();
    }
}

}