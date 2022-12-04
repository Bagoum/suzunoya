using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Suzunoya.ControlFlow;
using Suzunoya.Data;

namespace Suzunoya.ADV {

/// <summary>
/// Service that manages the execution of an ADV context.
/// </summary>
[PublicAPI]
public class ADVManager : ITokenized {
    /// <summary>
    /// Enum describing the state that governs what interactions the player may do.
    /// </summary>
    public enum State {
        /// <summary>
        /// Investigation state: the player can interact with objects in the environment.
        /// </summary>
        Investigation = 0,
        /// <summary>
        /// Dialogue state: the VN is executing dialogue.
        /// </summary>
        Dialogue = 100,
        /// <summary>
        /// Waiting state: Some loading process is occurring that disables player interaction.
        /// </summary>
        Waiting = 200
    }
    /// <inheritdoc cref="IExecutingADV.ADVData"/>
    public ADVData ADVData => ExecAdv!.ADVData;
    /// <inheritdoc cref="IExecutingADV.VN"/>
    public IVNState VNState => ExecAdv!.VN;
    /// <summary>
    /// The currently executing ADV process.
    /// </summary>
    public IExecutingADV? ExecAdv { get; private set; }
    
    /// <summary>
    /// The current <see cref="State"/> of the game.
    /// </summary>
    public DisturbedEvented<State> ADVState { get; } = new DisturbedFold<State>(State.Investigation, 
        (x, y) => (x > y) ? x : y);

    /// <inheritdoc/>
    public List<IDisposable> Tokens { get; } = new();

    /// <summary>
    /// Event invoked right before VN execution begins. Bool: whether or not parallel investigation is permitted.
    /// </summary>
    public Event<bool> VNExecutionStarting { get; } = new();

    /// <summary>
    /// Destroy the currently running ADV instance, if it exists.
    /// </summary>
    public void DestroyCurrentInstance() {
        ExecAdv?.Inst.Cancel();
    }
    
    /// <summary>
    /// Set the provided ADV execution as the current executing ADV.
    /// <br/>(Only one <see cref="IExecutingADV"/> may be handled by this service at a time.)
    /// </summary>
    public void SetupInstance(IExecutingADV inst) {
        DestroyCurrentInstance();
        ExecAdv = inst;
    }

    /// <summary>
    /// Update and retrieve the save data that should be serialized to disk.
    /// <br/>This may not be <see cref="ADVData"/> in cases where the current VN location is unidentifiable or
    ///  within a locked context (<see cref="StrongBoundedContext{T}.LoadSafe"/> = false).
    /// </summary>
    public ADVData GetSaveReadyADVData() {
        VNState.UpdateInstanceData();
        //If saving within an unlocateable VN execution, then use the unmodified save data for safety
        if (VNState.InstanceData.Location is null && ADVData.UnmodifiedSaveData is not null) {
            return ADVData.GetUnmodifiedSaveData() ?? throw new Exception("Couldn't load unmodified save data");
        //Otherwise, if we're in a bounded context with LoadSafe=false, then use the save data made right before entering the context
        } else if (ADVData.LockedContextData is { })
            return ADVData.GetLockedSaveData() ?? throw new Exception("Couldn't retrieve locked save data");
        return ADVData;
    }

    /// <summary>
    /// Execute a top-level VN segment. May fail with null if a VN segment is already executing.
    /// </summary>
    public Task<T>? TryExecuteVN<T>(BoundedContext<T> task, bool allowParallelInvestigation = false) {
        VNState.Flush();
        if (VNState.Contexts.Count > 0)
            return null;
        return ExecuteVN(task, allowParallelInvestigation);
    }

    /// <summary>
    /// Execute a top-level VN segment.
    /// </summary>
    public async Task<T> ExecuteVN<T>(BoundedContext<T> task, bool allowParallelInvestigation = false) {
        //Do this first in order to dispose dependencies on investigation state, such as interactable mimics
        // running BCTX on hover
        using var _ = ADVState.AddConst(allowParallelInvestigation ? State.Investigation : State.Dialogue);
        var vn = VNState;
        vn.Flush();
        if (vn.Contexts.Count > 0)
            throw new Exception($"Executing a top-level VN segment {task.ID} when one is already active");
        if (ADVData.UnmodifiedSaveData != null)
            throw new Exception($"Executing a top-level VN segment {task.ID} when unmodifiedSaveData is non-null");
        vn.ResetInterruptStatus();
        var inst = ExecAdv ?? throw new Exception();
        VNExecutionStarting.OnNext(allowParallelInvestigation);
        vn.Logs.OnNext($"Starting VN segment {task.ID}");
        ADVData.PreserveData();
        try {
            var res = await task;
            vn.UpdateInstanceData();
            return res;
        } catch (Exception e) {
            if (e is OperationCanceledException)
                vn.Logs.OnNext($"Cancelled VN segment {task.ID}");
            else
                vn.Logs.OnNext(e);
            throw;
        } finally {
            vn.ResetInterruptStatus();
            ADVData.RemovePreservedData();
            vn.Logs.OnNext($"Completed VN segment {task.ID}. Final state: {inst.Inst.Tracker.ToCompletion()}");
            //TODO: require a smarter way to handle "reverting to previous state"
        }
    }

}
}