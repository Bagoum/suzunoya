using System;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using Suzunoya.Data;

namespace Suzunoya.ControlFlow {

/// <summary>
/// A representation of a currently-executing <see cref="BoundedContext{T}"/> within a <see cref="IVNState"/>.
/// </summary>
public interface OpenedContext {
    /// <summary>
    /// True iff <see cref="Data"/> is saved to the VN instance data on completion.
    /// <br/>(By default, this is <see cref="BCtx"/>.<see cref="IBoundedContext.Identifiable"/>.)
    /// </summary>
    public bool DataIsSaved => BCtx.Identifiable;
    
    /// <summary>
    /// The bounded context that is currently being executed.
    /// </summary>
    public IBoundedContext BCtx { get; }
    
    /// <summary>
    /// The joint cancellee of the executing <see cref="BoundedContext{T}"/>'s
    ///  <see cref="BoundedContext{T}.LocalCToken"/> with all executing parent context's tokens.
    /// </summary>
    public ICancellee? CtxCToken { get; }
    
    /// <summary>
    /// The local data of the bounded context
    /// </summary>
    public BoundedContextData Data { get; }
    /// <inheritdoc cref="IBoundedContext.ID"/>
    public string ID => BCtx.ID;

    /// <summary>
    /// Link <see cref="Data"/> to the data in the provided <see cref="IInstanceData"/>.
    /// </summary>
    public void RemapData(IInstanceData src);
}

/// <inheritdoc cref="OpenedContext"/>
public class OpenedContext<T> : OpenedContext, IDisposable {
    private readonly IVNState vn;
    
    /// <inheritdoc cref="OpenedContext.BCtx"/>
    public BoundedContext<T> BCtx { get; }
    IBoundedContext OpenedContext.BCtx => BCtx;
    /// <inheritdoc cref="OpenedContext.Data"/>
    public BoundedContextData<T> Data { get; private set; }
    BoundedContextData OpenedContext.Data => Data;
    private OpenedContext? Parent { get; }
    
    /// <inheritdoc />
    public ICancellee? CtxCToken { get; }
    
    /// <summary>
    /// Open a bounded context in the executing VN state,
    /// </summary>
    public OpenedContext(BoundedContext<T> bCtx) {
        this.vn = bCtx.VN;
        this.BCtx = bCtx;
        vn.ContextStarted.OnNext(this);
        if ((this as OpenedContext).DataIsSaved) {
            if (vn.Contexts.Count > 0) {
                Data = (Parent = vn.Contexts[^1]).Data.CreateNested(bCtx);
            } else
                Data = vn.InstanceData.CreateBCtxData(bCtx);
        } else
            Data = new(bCtx.ID, Maybe<T>.None, new(), new());
        CtxCToken = JointCancellee.MaybeFrom(Parent?.CtxCToken, bCtx.LocalCToken);
        vn.Contexts.Add(this);
    }

    /// <inheritdoc/>
    public void RemapData(IInstanceData src) {
        if (Parent == null)
            Data = src.GetBCtxData<T>(Data.Key);
        else {
            Parent.RemapData(src);
            Data = Parent.Data.GetNested<T>(Data.Key);
        }
            
    }

    /// <inheritdoc />
    public void Dispose() {
        if (vn.LowestContext != this)
            throw new Exception("Contexts closed in wrong order. This is an engine error. Please report this.");
        vn.Contexts.RemoveAt(vn.Contexts.Count - 1);
        vn.ContextFinished.OnNext(this);
    }

    /// <inheritdoc />
    public override string ToString() => BCtx.ToString();
}
}