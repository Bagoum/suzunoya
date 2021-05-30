using System;
using System.Drawing;
using System.Numerics;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Suzunoya.ControlFlow;
using Suzunoya.Display;

namespace Suzunoya.Entities {
public interface IRendered : ITransform {
    /// <summary>
    /// A basic plugin setup would be to associate a RenderGroup  with an engine camera and game-object layer,
    ///  then treat RenderLayer and SortingID as equivalent to engine "sorting layer" / "order in layer".
    /// </summary>
    Evented<RenderGroup?> RenderGroup { get; }
    Evented<int> RenderLayer { get; }
    Evented<int> SortingID { get; }
    Evented<bool> Visible { get; }
    Evented<FColor> Tint { get; }
    
    /// <summary>
    /// If the object is already associated with a render group,
    /// then it should deattach from the existing render group.
    /// </summary>
    void AddToRenderGroup(RenderGroup group, int? sortingID = null);
}

public class Rendered : Transform, IRendered {
    public Evented<RenderGroup?> RenderGroup { get; } = new(null);
    private IDisposable? renderGroupToken;
    public Evented<int> RenderLayer { get; }
    public Evented<int> SortingID { get; } = new(0);
    public Evented<bool> Visible { get; }
    public Evented<FColor> Tint { get; }

    public float Alpha {
        get => Tint.Value.a;
        set => Tint.Value = Tint.Value.WithA(value);
    }

    /// <summary>
    /// Note: this should be "effectively static" as it is called in the constructor.
    /// </summary>
    protected virtual int DefaultRenderLayer => 0;

    public Rendered(Vector3? location = null, Vector3? eulerAnglesD = null, Vector3? scale = null, 
        bool visible = false, FColor? color = null) : base(location, eulerAnglesD, scale) {
        // ReSharper disable once VirtualMemberCallInConstructor
        RenderLayer = new(DefaultRenderLayer);
        
        Visible = new(visible);
        Tint = new(color ?? Color.White);
    }
    
    public void AddToRenderGroup(RenderGroup group, int? sortingID = null) {
        if (group.Container != Container)
            throw new Exception($"Cannot add rendered {this} to a render group in a different VNState");
        SortingID.PublishIfNotSame(sortingID ?? group.NextSortingID());
        renderGroupToken?.Dispose();
        renderGroupToken = group.Add(this);
        RenderGroup.Value = group;
    }

    public override void Delete() {
        renderGroupToken?.Dispose();
        renderGroupToken = null;
        base.Delete();
    }
}

}