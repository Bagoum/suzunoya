using System;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using JetBrains.Annotations;
using Suzunoya.ADV;
using Suzunoya.Data;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {

/// <summary>
/// The method that the VN should use to handle a BCTX when its execution data already exists.
/// </summary>
[PublicAPI]
public enum RepeatContextExecution {
    /// <summary>
    /// Throw an exception.
    /// </summary>
    Fail = 0,
    
    /// <summary>
    /// Reuse the existing BCTX data.
    /// </summary>
    Reuse = 1,
    
    /// <summary>
    /// Clear the existing BCTX data.
    /// </summary>
    Reset = 2,
}

/// <summary>
/// Non-generic base class for <see cref="BoundedContext{T}"/>
/// </summary>
[PublicAPI]
public interface IBoundedContext {
    /// <summary>
    /// The <see cref="IVNState"/> within which the bounded context is executing.
    /// </summary>
    public IVNState VN { get; }
    /// <summary>
    /// An identifier for the bounded context.
    /// If this is empty, then the context will be considered unidentifiable,
    ///  and features such as save/load will be disabled.
    /// </summary>
    public string ID { get; }
    /// <summary>
    /// Whether this bounded context has an identifier that allows it to be identified for save/load and persistent
    ///  data storage in BCTXData.
    /// </summary>
    public bool Identifiable { get; }
    
    /// <inheritdoc cref="RepeatContextExecution"/>
    public RepeatContextExecution OnRepeat { get; }
}

/// <summary>
/// Non-generic base class for <see cref="StrongBoundedContext{T}"/>
/// </summary>
public interface IStrongBoundedContext : IBoundedContext {
    /// <summary>
    /// Whether or not it is safe to save/load inside this BCTX. This may be false in cases where the BCTX
    ///  contains nondeterministic code.
    /// <br/>This should be consumed by game logic handlers to call <see cref="ADVData.LockContext"/>.
    /// </summary>
    public bool LoadSafe { get; }
}

/// <summary>
/// A representation of a (possibly nested) task run on a VNState.
/// <br/>The return value of the contained task is automatically saved in the VNState when this runs to completion.
/// </summary>
/// <param name="VN">VN on which to run the task</param>
/// <param name="ID">Identifier for the data produced by running the task</param>
/// <param name="InnerTask">Task to run</param>
/// <typeparam name="T">Type of the return value of the contained task</typeparam>
[PublicAPI]
public record BoundedContext<T>(IVNState VN, string ID, Func<Task<T>> InnerTask) : IBoundedContext {
    IVNState IBoundedContext.VN => VN;
    /// <summary>
    /// Task to run
    /// </summary>
    internal Func<Task<T>> InnerTask { get; init; } = InnerTask;

    /// <summary>
    /// A cancellation token that affects all VN operations run during the lifetime of this BCTX.
    /// </summary>
    public ICancellee? LocalCToken { get; init; } = null;
    
    /// <inheritdoc/>
    public bool Identifiable => !string.IsNullOrWhiteSpace(ID);

    /// <inheritdoc/>
    public RepeatContextExecution OnRepeat { get; set; } = RepeatContextExecution.Reuse;

    /// <summary>
    /// True iff there are no save-data changes in the body of the bounded context.
    /// <br/>This allows speed optimizations during ADV execution.
    /// </summary>
    public bool Trivial { get; init; } = false;

    /// <summary>
    /// Return true if the context has been executed and completed in the given parentage path.
    /// </summary>
    public bool IsCompletedInContexts(params string[] parents) => 
        VN.TryGetContextData<T>(out var res, parents.Append(ID).ToArray()) && res.Result.Valid;

    /// <summary>
    /// Run the contents of this bounded context on the VN.
    /// </summary>
    public Task<T> Execute() => VN.ExecuteContext(this);

    /// <summary>
    /// Syntactic sugar for `await ctx.Execute()`.
    /// </summary>
    [UsedImplicitly]
    public TaskAwaiter<T> GetAwaiter() => Execute().GetAwaiter();

    /// <inheritdoc/>
    public override string ToString() => $"Context:{ID}";
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
/// <param name="VN">VN on which to run the task</param>
/// <param name="ID">Identifier for the data produced by running the task</param>
/// <param name="InnerTask">Task to run</param>
/// <param name="ShortCircuit">Code to run if the BCTX is short-circuit that should mimic any nontransient
///  changes to the world state.</param>
/// <param name="OnFinish">Code to run at the end of the BCTX execution. Also run if short-circuit.
///  Useful for encoding state changes without duplicating them in <see cref="ShortCircuit"/>.</param>
/// <typeparam name="T">Type of the return value of the contained task</typeparam>
public record StrongBoundedContext<T>(IVNState VN, string ID, Func<Task<T>> InnerTask, Action? ShortCircuit = null, Action? OnFinish = null) : BoundedContext<T>(VN, ID, InnerTask), IStrongBoundedContext {

    /// <summary>
    /// Default value to provide for this bounded context if it needs to be skipped during loading,
    ///  but has not saved a result value in the instance save.
    /// <br/>This only occurs if execution of the context was limited by an if statement/etc
    ///  without a ComputeFlag guard, or if an update was made to the game.
    /// </summary>
    public Maybe<T> LoadingDefault { get; init; } = Maybe<T>.None;
    
    /// <inheritdoc/>
    public bool LoadSafe { get; init; } = true;
    
    /// <inheritdoc/>
    public override string ToString() => $"StrongContext:{ID}";
}

}