using System;
using BagoumLib;
using BagoumLib.Cancellation;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {

/// <summary>
/// A disposable-ish token returned when the current VN execution is interrupted.
/// <br/>To end the interruption, "dispose" this token by calling <see cref="ReturnInterrupt"/>.
/// </summary>
public interface IVNInterruptionToken {
    /// <summary>
    /// Finish interrupting the enclosing process layer.
    /// <br/>Has no effect if called a second time.
    /// </summary>
    /// <param name="resultStatus">Either <see cref="InterruptionStatus.Continue"/> (continue the enclosing process)
    ///  or <see cref="InterruptionStatus.Abort"/> (stop the enclosing process).</param>
    void ReturnInterrupt(InterruptionStatus resultStatus);
    
    /// <summary>
    /// The status provided to <see cref="ReturnInterrupt"/> (or null if it has not been called yet).
    /// </summary>
    InterruptionStatus? FinalStatus { get; }
}

/// <summary>
/// An executing process layer on a VN, described by <see cref="VNInterruptionLayer"/>,
///  may be interrupted by another process layer (<see cref="VNState.Interrupt"/>) and later resumed.
/// </summary>
public class VNInterruptionLayer {
    /// <summary>
    /// VN on which this process layer is running.
    /// </summary>
    public IVNState VN { get; }
    /// <summary>
    /// The parent process layer that this process layer interrupted.
    /// </summary>
    public VNInterruptionLayer? Parent { get; }
    /// <summary>
    /// The current status of the process layer.
    /// </summary>
    public InterruptionStatus Status { get; set; } = InterruptionStatus.Normal;
    /// <summary>
    /// The <see cref="ICancellee"/> skip level corresponding to <see cref="Status"/>.
    /// </summary>
    public int InducedOperationCancelLevel => Status switch {
        //Skip forward to end the current subop while an op is interrupted
        InterruptionStatus.Interrupted => ICancellee.SoftSkipLevel,
        InterruptionStatus.Abort => ICancellee.HardCancelLevel,
        _ => 0
    };
    
    /// <summary>
    /// The interruption currently interrupting this process layer.
    /// </summary>
    public IVNInterruptionToken? InterruptedBy { get; internal set; } = null;

    /// <summary>
    /// The currently-executing processes on this process layer, all sharing a single skip/confirm token.
    /// </summary>
    public VNProcessGroup? CurrentProcesses { get; private set; }
    /// <summary>
    /// <see cref="CurrentProcesses"/>?.<see cref="VNProcessGroup.ConfirmToken"/>
    /// </summary>
    public ICancellee? ConfirmToken => CurrentProcesses?.ConfirmToken;

    /// <summary>
    /// Create a new process layer. (Should only be called by <see cref="VNState"/>)
    /// </summary>
    internal VNInterruptionLayer(IVNState vn, VNInterruptionLayer? parent) {
        this.VN = vn;
        this.Parent = parent;
    }
    
    /// <summary>
    /// Get the currently-executing process group, or create a new one if none exists.
    /// </summary>
    public VNProcessGroup GetOrMakeProcessGroup() {
        if (CurrentProcesses == null || CurrentProcesses.OperationCTokenDependencies == 0) {
            CurrentProcesses = new(this, true);
        }
        return CurrentProcesses;
    }
    
    /// <summary>
    /// Send a confirm input to the currently-executing process group.
    /// </summary>
    public void DoConfirm() => CurrentProcesses?.DoConfirm();
    
    
}

}