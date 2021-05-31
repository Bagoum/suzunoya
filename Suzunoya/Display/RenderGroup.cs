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
/// </summary>
public class RenderGroup : Transform {
    public const string DEFAULT_KEY = "$default";
    public string Key { get; }
    public Evented<int> Priority { get; }
    public Evented<bool> Visible { get; }

    public Evented<float> Zoom { get; } = new(1);
    public Evented<Vector3> ZoomTarget { get; } = new(Vector3.Zero);
    public ComputedEvented<Vector3> ZoomTransformOffset { get; }
    
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
        var m = -1;
        for (int ii = 0; ii < Contents.Count; ++ii) {
            if (Contents.ExistsAt(ii))
                m = Math.Max(m, Contents[ii].SortingID);
        }
        return m + 1;
    }

    private Cancellable? transitionToken;

    public ICancellee GetTransitionToken() {
        transitionToken?.Cancel();
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