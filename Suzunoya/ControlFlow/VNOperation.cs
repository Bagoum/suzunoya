using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using JetBrains.Annotations;

namespace Suzunoya.ControlFlow {

//LazyAwaitable are lazy wrappers around tasks that are not started until they are awaited.
//This allows chaining then in ways that are a bit difficult for tasks.
public interface LazyAwaitable {
    public Task Task { get; }
}

/// <summary>
/// A generic task that is not started until it is awaited.
/// </summary>
public record LazyTask(Func<Task> lazyTask) : LazyAwaitable {
    private Task? loadedTask;
    public Task Task => loadedTask ??= lazyTask();
}

/// <summary>
/// The task that is produced when waiting for a confirmation signal.
/// This is similar to VNOperation, but is not bounded by an operation canceller.
/// </summary>
public record VNComfirmTask(VNOperation? preceding, Func<Task> t) : LazyAwaitable {
    private Task? loadedTask;
    public Task Task => loadedTask ??= _AsTask();

    private async Task _AsTask() {
        if (preceding != null)
            await preceding;
        await t();
    }
    
    
    [UsedImplicitly]
    public TaskAwaiter GetAwaiter() => Task.GetAwaiter();
}

/// <summary>
/// A VNOperation is a sequential sequence of tasks bounded by one common operation cancellation token.
/// If multiple VNOperations are run at the same time, they may end up sharing the same operation token.
/// Tasks batched under a VNOperation do not need to check cancellation at their start or end.
/// </summary>
public record VNOperation : LazyAwaitable {
    public IVNState VN { get; }
    private IVNExecCtx VNExec { get; }
    public Func<ICancellee, Task>[] Suboperations { get; }

    private Task? loadedTask;
    public Task Task => loadedTask ??= _AsTask();
    public VNComfirmTask C => VN.SpinUntilConfirm(this);

    public VNOperation(IVNState vn, IVNExecCtx? exec = null, params Func<ICancellee, Task>[] suboperations) {
        this.VN = vn;
        this.VNExec = exec ?? VN.ExecCtx;
        this.Suboperations = suboperations;
    }

    private async Task _AsTask() {
        using var d = VN.ExecCtx.GetOperationCanceller(out var cT);
        cT.ThrowIfHardCancelled();
        foreach (var t in Suboperations) {
            await t(cT);
            cT.ThrowIfHardCancelled();
        }
    }

    public VNOperation Then(params Func<ICancellee, Task>[] nxt) => 
        new(VN, VNExec, Suboperations.Concat(nxt).ToArray());

    public VNOperation Then(VNOperation nxt) =>
        VN == nxt.VN ?
            Then(nxt.Suboperations) :
            throw new Exception($"Cannot sequence VNOperations across different VNStates: {VN}, {nxt.VN}");

    public VNOperation And(VNOperation nxt) => Parallel(this, nxt);
    
    
    public static VNOperation Parallel(IVNState vn, params Func<ICancellee, Task>[] subops) =>
        new VNOperation(vn, vn.ExecCtx, ct => Task.WhenAll(subops.Select(s => s(ct))));
    
    public static VNOperation Parallel(params VNOperation[] vnos) {
        for (int ii = 1; ii < vnos.Length; ++ii) {
            if (vnos[ii].VN != vnos[0].VN || vnos[ii].VNExec != vnos[0].VNExec)
                throw new Exception(
                    $"Cannot parallelize VNOperations across different VNStates: {vnos[0].VN}, {vnos[ii].VN}");

        }
        return new VNOperation(vnos[0].VN, vnos[0].VNExec, Parallel(vnos.Select(vno => Sequential(vno.Suboperations))));
    }

    public static Func<T, Task> Parallel<T>(params Func<T, Task>[] tasks) =>
        x => Task.WhenAll(tasks.Select(t => t(x)));
    public static Func<T, Task> Parallel<T>(IEnumerable<Func<T, Task>> tasks) =>
        x => Task.WhenAll(tasks.Select(t => t(x)));


    public static Func<ICancellee, Task> Sequential(params Func<ICancellee, Task>[] tasks) =>
        Sequential((IEnumerable<Func<ICancellee, Task>>)tasks);
    
    public static Func<ICancellee, Task> Sequential(IEnumerable<Func<ICancellee, Task>> tasks) {
        async Task inner(ICancellee x) {
            x.ThrowIfHardCancelled();
            foreach (var t in tasks) {
                x.ThrowIfHardCancelled();
                await t(x);
            }
        }
        return inner;
    }

    public override string ToString() => $"VNOp (Len: {Suboperations.Length})";

    [UsedImplicitly]
    public TaskAwaiter GetAwaiter() => Task.GetAwaiter();
}
}