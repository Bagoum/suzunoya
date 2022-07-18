using System;
using BagoumLib.Functional;
using Suzunoya.Data;

namespace Suzunoya.ControlFlow {

public interface OpenedContext {
    public BoundedContext BCtx { get; }
    public BoundedContextData Data { get; }
    public string ID => BCtx.ID;

    /// <summary>
    /// Link <see cref="Data"/> to the data in the provided <see cref="IInstanceData"/>.
    /// </summary>
    public void RemapData(IInstanceData src);
}

/// <summary>
/// A representation of a currently-executing <see cref="BoundedContext{T}"/> within a <see cref="IVNState"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public class OpenedContext<T> : OpenedContext, IDisposable {
    private readonly IVNState vn;
    public BoundedContext<T> BCtx { get; }
    BoundedContext OpenedContext.BCtx => BCtx;
    public BoundedContextData<T> Data { get; private set; }
    BoundedContextData OpenedContext.Data => Data;
    private OpenedContext? Parent { get; }
    public OpenedContext(VNState vn, BoundedContext<T> bCtx) {
        this.vn = vn;
        this.BCtx = bCtx;
        Data = new BoundedContextData<T>(bCtx.ID, Maybe<T>.None, new KeyValueRepository(), new());
        if (vn.Contexts.Count > 0) {
            (Parent = vn.Contexts[^1]).Data.SaveNested(Data, vn.AllowsRepeatContextExecution);
        } else
            vn.InstanceData.SaveBCtxData(Data, vn.AllowsRepeatContextExecution);
        vn.Contexts.Add(this);
        vn.ContextStarted.OnNext(this);
    }

    public void RemapData(IInstanceData src) {
        if (Parent == null)
            Data = src.GetBCtxData<T>(Data.Key);
        else {
            Parent.RemapData(src);
            Data = Parent.Data.GetNested<T>(Data.Key);
        }
            
    }

    public void Dispose() {
        if (vn.LowestContext != this)
            throw new Exception("Contexts closed in wrong order. This is an engine error. Please report this.");
        vn.Contexts.RemoveAt(vn.Contexts.Count - 1);
        vn.ContextFinished.OnNext(this);
    }

    public override string ToString() => BCtx.ToString();
}
}