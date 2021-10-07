using System;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {

/// <summary>
/// Non-generic interface for BoundedContext&lt;T&gt;.
/// </summary>
public interface IBoundedContext {
    VNState VN { get; }
    string ID { get; }
}

/// <summary>
/// A representation of a task which, if executed to completion, has no lasting effects on the game state
///  other than modifications made to the VNState save data or logs.
/// <br/>In other words, a bounded context is a task that "cleans up after itself",
///  so it destroys any objects that it creates.
/// <br/>When skipping the game state to various points for usages such as loading or backlogging,
///  the engine may skip the entirety of a BoundedContext.
/// <br/>The return value of the contained task is automatically saved in the VNState when this runs to completion.
/// </summary>
/// <typeparam name="T"></typeparam>
public class BoundedContext<T> : IBoundedContext {
    public VNState VN { get; }
    public string ID { get; }
    public ILazyAwaitable<T> Task { get; }
    public BoundedContext(VNState vn, string? id, ILazyAwaitable<T> task) {
        VN = vn;
        ID = id ?? "";
        Task = task;
    }

    public BoundedContext(VNState vn, string? id, Func<Task<T>> task) : this(vn, id, new LazyTask<T>(task)) { }

    public ILazyAwaitable<T> Execute() => VN.ExecuteContext(this);

    public override string ToString() => $"Context:{ID}";
}

}