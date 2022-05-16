using System;
using BagoumLib;
using BagoumLib.Cancellation;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {

/// <summary>
/// A disposable-ish token returned when the current VN execution is interrupted.
/// </summary>
public interface IVNInterruptionToken {
    /// <summary>
    /// Finish interrupting the enclosing process.
    /// <br/>Has no effect if called a second time.
    /// </summary>
    /// <param name="resultStatus">Either <see cref="InterruptionStatus.Normal"/> (continue the enclosing process)
    ///  or <see cref="InterruptionStatus.Abort"/> (stop the enclosing process).</param>
    void ReturnInterrupt(InterruptionStatus resultStatus);
}

/// <summary>
/// An executing process on a VN, described by <see cref="VNInterruptionLayer"/>,
///  may be interrupted by another process (<see cref="VNState.Interrupt"/>) and later resumed.
/// </summary>
public class VNInterruptionLayer {
    public IVNState VN { get; }
    public VNInterruptionLayer? Parent { get; }
    public InterruptionStatus Interruption { get; set; } = InterruptionStatus.Normal;
    public int InducedOperationCancelLevel => Interruption switch {
        //Skip forward to end the current subop while an op is interrupted
        InterruptionStatus.Interrupted => ICancellee.SoftSkipLevel,
        InterruptionStatus.Abort => ICancellee.HardCancelLevel,
        _ => 0
    };
    
    private ProcessGroupInterruption? interrupter = null;
    
    public VNProcessGroup? CurrentProcesses { get; private set; }
    public ICancellee? ConfirmToken => CurrentProcesses?.ConfirmToken;

    public VNInterruptionLayer(IVNState vn, VNInterruptionLayer? parent) {
        this.VN = vn;
        this.Parent = parent;
    }

    public VNProcessGroup GetOrMakeProcessGroup() {
        if (CurrentProcesses == null || CurrentProcesses.OperationCTokenDependencies == 0) {
            CurrentProcesses = new(this, true);
        }
        return CurrentProcesses;
    }
    public void DoConfirm() => CurrentProcesses?.DoConfirm();
    
    public ProcessGroupInterruption Interrupt() {
        if (CurrentProcesses != null) 
            CurrentProcesses.WasInterrupted = true;
        return interrupter ??= new ProcessGroupInterruption(this);
    }

    public class ProcessGroupInterruption {
        public VNInterruptionLayer Layer { get; }

        public ProcessGroupInterruption(VNInterruptionLayer layer) {
            this.Layer = layer;
            layer.Interruption = InterruptionStatus.Interrupted;
        }

        public void ReturnInterrupt(InterruptionStatus resultStatus) {
            if (resultStatus is not (InterruptionStatus.Abort or InterruptionStatus.Continue))
                throw new Exception($"Interrupt status must be abort or continue, not {resultStatus}");
            if (Layer.interrupter == this) {
                Layer.Interruption = resultStatus;
                Layer.interrupter = null;
            }
        }
    }
    
}

}