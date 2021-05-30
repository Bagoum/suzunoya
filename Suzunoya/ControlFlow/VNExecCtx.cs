using System;
using BagoumLib.Cancellation;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {
public interface IVNExecCtx {
    string? ScriptID { get; }
    /// <summary>
    /// Any request to GetOperationCanceller is a suboperation.
    /// Suboperations are batched together as a single operation by sharing an operationCToken return value.
    /// Either count can be used as a save point. (Currently, SuboperationCount is used.)
    /// </summary>
    int SuboperationCount { get; }
    int OperationCount { get; }
    int Line { get; } // => SuboperationCount

    /// <summary>
    /// Create a cancellee that can be read for operation skipping (SkipOperation).
    /// When the suboperation is finished, dispose the disposable.
    /// </summary>
    IDisposable GetOperationCanceller(out ICancellee cT);

    /// <summary>
    /// Skip all operations, almost instantaneously, until Line == line.
    /// </summary>
    void LoadUntil(int line);
}
public class VNExecCtx : IVNExecCtx {
    private readonly VNState vn;
    private readonly VNExecCtx? ancestor;
    public string? ScriptID { get; }
    public Cancellable? operationCTS;
    public ICancellee? operationCToken;
    private int? loadToLine;
    public int operationCTokenDependencies { get; private set; } = 0;
    public int SuboperationCount { get; private set; } = 0;
    public int OperationCount { get; private set; } = 0;
    public int Line => SuboperationCount;
    //Let's say we're skipping to NESTER,10 where NESTER calls NESTEE on line 5. 
    //In that case, NESTEE will not receive a LoadToLine, but it should skip its entirety.
    public bool LoadSkipping => loadToLine > Line || (ancestor?.LoadSkipping == true);
    public bool Skipping => LoadSkipping; //TODO: other skips such as "skip all" or "skip read"

    public VNExecCtx(VNState vn, VNExecCtx? ancestor, string? scriptId) {
        this.vn = vn;
        this.ancestor = ancestor;
        this.ScriptID = scriptId;
    }

    public IDisposable GetOperationCanceller(out ICancellee cT) {
        ++SuboperationCount;
        if (operationCTS == null || operationCTokenDependencies <= 0) {
            ++OperationCount;
            operationCTS = new Cancellable();
            operationCToken = new JointCancellee(vn.CToken, operationCTS);
            if (Skipping) SkipOperation();
        }
        cT = operationCToken!;
        return new SubOpTracker(this);
    }

    public void SkipOperation() => operationCTS?.Cancel(CancelHelpers.SoftSkipLevel);
    public void LoadUntil(int line) {
        loadToLine = line;
        if (Skipping) SkipOperation();
    }

    private class SubOpTracker : IDisposable {
        private readonly VNExecCtx ctx;
        public SubOpTracker(VNExecCtx ctx) {
            this.ctx = ctx;
            ++ctx.operationCTokenDependencies;
        }

        public void Dispose() {
            --ctx.operationCTokenDependencies;
        }
    }
}
}