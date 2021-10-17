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
/// <br/>To allow for minor exceptions to the "no lasting effects" rule, a ShortCircuit function may be provided
/// that will be run in the full-skip case. This function should replicate any lasting effects.
/// </summary>
/// <typeparam name="T"></typeparam>
public class BoundedContext<T> : IBoundedContext {
    public VNState VN { get; }
    public string ID { get; }
    public ILazyAwaitable<T> _InnerTask { get; }
    public Action? ShortCircuit { get; }
    public BoundedContext(VNState vn, string? id, ILazyAwaitable<T> innerTask, Action? shortCircuit = null) {
        VN = vn;
        ID = id ?? "";
        _InnerTask = innerTask;
        ShortCircuit = shortCircuit;
    }

    public BoundedContext(VNState vn, string? id, Func<Task<T>> task, Action? shortCircuit = null) : 
        this(vn, id, new LazyTask<T>(task), shortCircuit) { }

    public ILazyAwaitable<T> Execute() => VN.ExecuteContext(this);

    public override string ToString() => $"Context:{ID}";
}

}