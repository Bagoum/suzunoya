using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
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
    public Evented<bool> Visible { get; }
    public DisturbedProduct<FColor> Tint { get; } = new(new FColor(1, 1, 1, 1));
    
    public float Alpha {
        get => Tint.Value.a;
        set => Tint.Value = Tint.BaseValue.WithA(value);
    }

    public Evented<float> Zoom { get; } = new(1);
    public Evented<Vector3> ZoomTarget { get; } = new(Vector3.Zero);
    public LazyEvented<Vector3> ZoomTransformOffset { get; }
    
    public DMCompactingArray<IRendered> Contents { get; } = new();
    public Event<IRendered> RendererAdded { get; } = new();

    public RenderGroup(IVNState container, string key = DEFAULT_KEY, int priority = 0, bool visible = false) {
        ZoomTransformOffset = new(() => (Zoom - 1) / Zoom * (ZoomTarget.Value - Location), 
            new UnitEventProxy<float>(Zoom), new UnitEventProxy<Vector3>(ZoomTarget));
        Container = container;
        Key = key;
        Priority = new(priority);
        Visible = new(visible);
        tokens.Add(Container._AddRenderGroup(this));
    }

    public override void AddToVNState(IVNState container) =>
        throw new Exception("Do not call RenderGroup.AddToVNState. Provide the VNState in the constructor.");

    public IDisposable Add(IRendered rnd) {
        var dsp = Contents.Add(rnd);
        RendererAdded.OnNext(rnd);
        return dsp;
    }

    public int NextSortingID() {
        var m = -1 * DefaultSortingIDStep;
        for (int ii = 0; ii < Contents.Count; ++ii) {
            if (Contents.ExistsAt(ii))
                m = Math.Max(m, Contents[ii].SortingID.BaseValue);
        }
        return m + DefaultSortingIDStep;
    }

    private Cancellable? transitionToken;

    public ICancellee GetTransitionToken() {
        transitionToken?.Cancel(ICancellee.HardCancelLevel);
        transitionToken = new Cancellable();
        return new JointCancellee(LifetimeToken, transitionToken);
    }

    public override void Delete() {
        for (int ii = 0; ii < Contents.Count; ++ii) {
            if (Contents.ExistsAt(ii))
                Contents[ii].Delete();
        }
        base.Delete();
    }
}

}