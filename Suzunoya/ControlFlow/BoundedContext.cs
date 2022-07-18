using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using JetBrains.Annotations;
using Suzunoya.Data;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {

/// <summary>
/// Non-generic base class for BoundedContext&lt;T&gt;.
/// </summary>
[PublicAPI]
public abstract class BoundedContext {
    public IVNState VN { get; init; }
    /// <summary>
    /// If this is empty, then the context will be considered unidentifiable.
    /// </summary>
    public string ID { get; init; }
    public BoundedContext(VNState vn, string id) {
        VN = vn;
        ID = id;
    }
}

/// <summary>
/// A <see cref="BoundedContext{T}"/> with the guarantee that any lasting effects on game state
///  produced by running this task are duplicated in <see cref="StrongBoundedContext{T}.ShortCircuit"/>.
/// <br/><see cref="VNState.SaveLocalValue{T}"/> does NOT need to be duplicated in ShortCircuit,
///  as it can be automatically handled in <see cref="BoundedContextData"/>. However, any other modifications
///  to instance data must be duplicated in ShortCircuit.
/// <br/>Note that if these are nested, then the outer SBC must also execute the short-circuit code
///  in all of its nested children in its own short-circuit.
/// <br/>When skipping the game state to various points for usages such as loading or backlogging,
///  the engine may skip the entirety of a StrongBoundedContext.
/// </summary>
/// <typeparam name="T"></typeparam>
public class StrongBoundedContext<T> : BoundedContext<T> {
    /// <summary>
    /// Code to run if the BCTX is short-circuit that should mimic any nontransient
    ///  changes to the world state.
    /// </summary>
    public Action? ShortCircuit { get; init; }
    
    /// <summary>
    /// Code to run at the end of the BCTX execution. Also run if short-circuit.
    ///  Useful for encoding state changes without duplicating them in <see cref="ShortCircuit"/>.
    /// </summary>
    public Action? OnFinish { get; init; }
    /// <summary>
    /// Default value to provide for this bounded context if it needs to be skipped during loading,
    ///  but has not saved a result value in the instance save.
    /// <br/>This only occurs if execution of the context was limited by an if statement/etc
    ///  without a ComputeFlag guard, or if an update was made to the game.
    /// </summary>
    public Maybe<T> LoadingDefault { get; init; } = Maybe<T>.None;
    
    
    public StrongBoundedContext(VNState vn, string id, Func<Task<T>> innerTask, Action? shortCircuit = null, Action? onFinish = null) : base(vn, id, innerTask) {
        ShortCircuit = shortCircuit;
        OnFinish = onFinish;
    }
}

/// <summary>
/// A representation of a (possibly nested) task run on a VNState.
/// <br/>The return value of the contained task is automatically saved in the VNState when this runs to completion.
/// <br/>All external awaited tasks should be wrapped in BoundedContext (see <see cref="VNState.Bound{T}"/>)
/// </summary>
/// <typeparam name="T">Type of the return value of the contained task</typeparam>
[PublicAPI]
public class BoundedContext<T> : BoundedContext {
    private Func<Task<T>> InnerTask { get; }

    public bool IsCompletedInContexts(params string[] contexts) => 
        VN.TryGetContextData<T>(out var res, contexts.Append(ID).ToArray()) && res.Result.Valid;
    
    public BoundedContext(VNState vn, string id, Func<Task<T>> innerTask) : base(vn, id) {
        InnerTask = innerTask;
    }

    internal Task<T> Execute() => VN.ExecuteContext(this, InnerTask);

    [UsedImplicitly]
    public TaskAwaiter<T> GetAwaiter() => Execute().GetAwaiter();

    public override string ToString() => $"Context:{ID}";
}

}