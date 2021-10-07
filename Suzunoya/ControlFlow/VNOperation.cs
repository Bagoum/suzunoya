using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Tasks;
using JetBrains.Annotations;

namespace Suzunoya.ControlFlow {

//LazyAwaitable are lazy wrappers around tasks that are not started until they are awaited.
//This allows chaining then in ways that are a bit difficult for tasks.
public interface ILazyAwaitable<T> {
    public Task<T> Task { get; }
    public TaskAwaiter<T> GetAwaiter(); // => Task.GetAwaiter();
}

/// <summary>
/// An function pretending to be a task.
/// </summary>
public record LazyFunc<T>(Func<T> lazyFunc) : ILazyAwaitable<T> {
    private Task<T>? loadedTask;
    public Task<T> Task => loadedTask ??= LazyTask();

    private Task<T> LazyTask() {
        var result = lazyFunc();
        return System.Threading.Tasks.Task.FromResult(result);
    }

    public TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();

    public static implicit operator LazyFunc<T>(Func<T> op) => new(op);
}

/// <summary>
/// An action predenting to be a task.
/// </summary>
public record LazyAction : LazyFunc<Unit> {

    public LazyAction(Action lazyOp) : base(() => {
        lazyOp();
        return Unit.Default;
    }) {
    }
    
    public static implicit operator LazyAction(Action op) => new(op);
    
}

/// <summary>
/// A generic task that is not started until it is awaited.
/// </summary>
public record LazyTask<T>(Func<Task<T>> lazyTask) : ILazyAwaitable<T> {
    private Task<T>? loadedTask;
    public Task<T> Task => loadedTask ??= lazyTask();
    
    public TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();
}
public record LazyTask : LazyTask<Unit> {
    public LazyTask(Func<Task> lazyTask) : base(async () => {
        await lazyTask();
        return Unit.Default;
    }) { }
}

/// <summary>
/// The task that is produced when waiting for a confirmation signal.
/// This is similar to VNOperation, but is not bounded by an operation canceller.
/// </summary>
public record VNConfirmTask(VNOperation? preceding, Func<Task<Completion>> t) : ILazyAwaitable<Completion> {
    private Task<Completion>? loadedTask;
    public Task<Completion> Task => loadedTask ??= _AsTask();

    private async Task<Completion> _AsTask() {
        if (preceding != null)
            await preceding;
        return await t();
    }
    
    
    [UsedImplicitly]
    public TaskAwaiter<Completion> GetAwaiter() => Task.GetAwaiter();
}

/// <summary>
/// A VNOperation is a sequential sequence of tasks bounded by one common operation cancellation token.
/// If multiple VNOperations are run at the same time, they may end up sharing the same operation token.
/// Tasks batched under a VNOperation do not need to check cancellation at their start or end.
/// </summary>
public record VNOperation : ILazyAwaitable<Completion> {
    public IVNState VN { get; }
    public Func<VNOpTracker, Task>[] Suboperations { get; init; }

    public bool AllowUserSkip { get; init; } = true;

    private Task<Completion>? loadedTask;
    public Task<Completion> Task => loadedTask ??= _AsTask();
    public VNConfirmTask C => VN.SpinUntilConfirm(this);

    public VNOperation(IVNState vn, params Func<VNOpTracker, Task>[] suboperations) {
        this.VN = vn;
        this.Suboperations = suboperations;
    }

    private async Task<Completion> _AsTask() {
        using var d = VN.GetOperationCanceller(out var cT, AllowUserSkip);
        cT.ThrowIfHardCancelled();
        var tracker = new VNOpTracker(VNLocation.Make(VN), cT);
        foreach (var t in Suboperations) {
            await t(tracker);
            cT.ThrowIfHardCancelled();
        }
        return cT.ToCompletion();
    }

    public VNOperation Then(Action nxt) {
        var nsubops = Suboperations.ToArray();
        var ft = nsubops[nsubops.Length - 1];
        nsubops[nsubops.Length - 1] = ct => ft(ct).ContinueWithSync(nxt);
        return this with {Suboperations = nsubops};
    }
    public VNOperation Then(params Func<ICancellee, Task>[] nxt) =>
        this with {Suboperations = Suboperations.Concat(nxt).ToArray()};

    private static void CheckUniformity(VNOperation[] vnos) {
        for (int ii = 1; ii < vnos.Length; ++ii) {
            if (vnos[ii].VN != vnos[0].VN)
                throw new Exception(
                    $"Cannot join VNOperations across different VNStates: {vnos[0].VN}, {vnos[ii].VN}");
        }
    }

    public VNOperation And(params VNOperation[] nxt) => Parallel(nxt.Prepend(this).ToArray());

    public VNOperation Then(params VNOperation[] nxt) {
        nxt = nxt.Prepend(this).ToArray();
        CheckUniformity(nxt);
        return new VNOperation(nxt[0].VN, nxt.SelectMany(vno => vno.Suboperations).ToArray()) {
            AllowUserSkip = nxt.All(v => v.AllowUserSkip)
        };
    }
    public static VNOperation Parallel(params VNOperation[] vnos) {
        CheckUniformity(vnos);
        return new VNOperation(vnos[0].VN, Parallel(vnos.Select(vno => Sequential(vno.Suboperations)))) {
            AllowUserSkip = vnos.All(v => v.AllowUserSkip)
        };
    }

    public static Func<T, Task> Parallel<T>(IEnumerable<Func<T, Task>> tasks) =>
        x => System.Threading.Tasks.Task.WhenAll(tasks.Select(t => t(x)));

    public static Func<CT, Task> Sequential<CT>(IEnumerable<Func<CT, Task>> tasks) where CT : ICancellee {
        async Task inner(CT x) {
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
    public TaskAwaiter<Completion> GetAwaiter() => Task.GetAwaiter();
}
}