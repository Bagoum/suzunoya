using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using JetBrains.Annotations;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {

/// <summary>
/// Non-generic interface for BoundedContext&lt;T&gt;.
/// </summary>
public interface IBoundedContext {
    IVNState VN { get; }
    string ID { get; }
}

/// <summary>
/// A representation of a task which, if executed to completion, has no lasting effects on the game state
///  other than modifications made to the VNState save data or logs.
/// <br/>In other words, a bounded context is a task that "cleans up after itself",
///  so it destroys any entities that it creates.
/// <br/>When skipping the game state to various points for usages such as loading or backlogging,
///  the engine may skip the entirety of a BoundedContext.
/// <br/>The return value of the contained task is automatically saved in the VNState when this runs to completion.
/// <br/>To allow for minor exceptions to the "no lasting effects" rule, a ShortCircuit function may be provided
/// that will be run in the full-skip case. This function should replicate any lasting effects.
/// <br/>All external awaited tasks should be wrapped in BoundedContext (see <see cref="VNState.Bound{T}"/>)
/// </summary>
/// <typeparam name="T">Type of the return value of the contained task</typeparam>
public class BoundedContext<T> : IBoundedContext {
    public IVNState VN { get; }
    public string ID { get; }
    private ILazyAwaitable<T> InnerTask { get; }
    public Action? ShortCircuit { get; init; }
    /// <summary>
    /// Default value to provide for this bounded context if it needs to be skipped during loading,
    ///  but has not saved a result value in the instance save.
    /// <br/>This only occurs if execution of the context was limited by an if statement/etc, or
    ///  an update was made to the game.
    /// </summary>
    public Maybe<T> LoadingDefault { get; init; } = Maybe<T>.None;
    
    public BoundedContext(VNState vn, string? id, ILazyAwaitable<T> innerTask, Action? shortCircuit = null) {
        VN = vn;
        ID = id ?? "";
        InnerTask = innerTask;
        ShortCircuit = shortCircuit;
    }

    public BoundedContext(VNState vn, string? id, Func<Task<T>> task, Action? shortCircuit = null) : 
        this(vn, id, new LazyTask<T>(task), shortCircuit) { }

    public ILazyAwaitable<T> Execute() => VN.ExecuteContext(this, InnerTask);
    
    [UsedImplicitly]
    public TaskAwaiter<T> GetAwaiter() => Execute().GetAwaiter();

    public override string ToString() => $"Context:{ID}";
}

}