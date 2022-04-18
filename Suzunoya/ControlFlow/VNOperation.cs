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
/// <summary>
/// A lazy wrapper around a task that is not started until it is awaited.
/// This allows chaining in ways that are a bit difficult for tasks.
/// </summary>
public interface ILazyAwaitable {
    Task Task { get; }
    TaskAwaiter GetAwaiter() => Task.GetAwaiter();
    ILazyAwaitable BoundCT(ICancellee cT);

    public static readonly ILazyAwaitable Null = new LazyAction(() => { });
}

/// <summary>
/// <see cref="ILazyAwaitable"/> with a specified task return type.
/// </summary>
public interface ILazyAwaitable<T> : ILazyAwaitable {
    Task ILazyAwaitable.Task => Task;
    ILazyAwaitable ILazyAwaitable.BoundCT(ICancellee cT) => BoundCT(cT);
    new Task<T> Task { get; }
    new TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();
    new ILazyAwaitable<T> BoundCT(ICancellee cT);
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

    public ILazyAwaitable<T> BoundCT(ICancellee cT) => new LazyFunc<T>(() => {
        if (cT.IsHardCancelled()) throw new OperationCanceledException();
        return lazyFunc();
    });

    public TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();

    public static implicit operator LazyFunc<T>(Func<T> op) => new(op);
    public VNOperation AsVnOp(IVNState vn) => new(vn, _ => Task);
}

/// <summary>
/// An action predenting to be a task.
/// </summary>
public record LazyAction : LazyFunc<Unit> {
    public LazyAction(Action lazyOp) : base(() => {
        lazyOp();
        return Unit.Default;
    }) { }
    
    public static implicit operator LazyAction(Action op) => new(op);
}


public record ParallelLazyAwaitable(params ILazyAwaitable[] tasks) : ILazyAwaitable {
    private Task? loadedTask;
    public Task Task => loadedTask ??= Task.WhenAll(tasks.Select(t => t.Task));
    public ILazyAwaitable BoundCT(ICancellee cT) =>
        new ParallelLazyAwaitable(tasks.Select(t => t.BoundCT(cT)).ToArray());
}

public record SequentialLazyAwaitable(params ILazyAwaitable?[] tasks) : ILazyAwaitable {
    private Task? loadedTask;
    public Task Task => loadedTask ??= LazyTask();

    public ILazyAwaitable BoundCT(ICancellee cT) =>
        new SequentialLazyAwaitable(tasks.Select(t => t?.BoundCT(cT)).ToArray());
    
    private async Task LazyTask() {
        foreach (var t in tasks)
            if (t != null)
                await t;
    }
}

/// <summary>
/// The task that is produced when waiting for a confirmation signal.
/// This is similar to VNOperation, but is not bounded by an operation canceller.
/// </summary>
public record VNConfirmTask(VNOperation? preceding, Func<ICancellee?, Task<Completion>> t) : ILazyAwaitable<Completion> {
    private Task<Completion>? loadedTask;
    public Task<Completion> Task => loadedTask ??= _AsTask();

    private async Task<Completion> _AsTask() {
        if (preceding != null)
            await preceding;
        return await t(null);
    }
    
    [UsedImplicitly]
    public TaskAwaiter<Completion> GetAwaiter() => Task.GetAwaiter();


    /// <summary>
    /// Create a copy of this awaitable that can be cancelled by the provided cancellation token.
    /// </summary>
    public VNConfirmTask BoundCT(ICancellee cT) => new(preceding?.BoundCT(cT), c => t(JointCancellee.From(c, cT)));
    ILazyAwaitable<Completion> ILazyAwaitable<Completion>.BoundCT(ICancellee cT) => BoundCT(cT);
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
    public Task<Completion> TaskWithCT(ICancellee cT) => loadedTask ??= _AsTask(cT);
    public VNConfirmTask C => VN.SpinUntilConfirm(this);

    public VNOperation(IVNState vn, params Func<VNOpTracker, Task>[] suboperations) {
        this.VN = vn;
        this.Suboperations = suboperations;
    }

    private async Task<Completion> _AsTask(ICancellee? cT = null) {
        using var d = VN.GetOperationCanceller(out var cT0, AllowUserSkip);
        cT = new JointCancellee(cT, cT0);
        cT.ThrowIfHardCancelled();
        var tracker = new VNOpTracker(VN, cT);
        foreach (var t in Suboperations) {
            await t(tracker);
            cT.ThrowIfHardCancelled();
        }
        return cT.ToCompletion();
    }

    public VNOperation Then(Action nxt) {
        var nsubops = Suboperations.ToArray();
        var ft = nsubops[^1];
        nsubops[^1] = ct => ft(ct).ContinueWithSync(nxt);
        return this with {Suboperations = nsubops};
    }
    public VNOperation Then(params Func<ICancellee, Task>[] nxt) =>
        this with {Suboperations = Suboperations.Concat(nxt).ToArray()};

    private static void CheckUniformity(VNOperation[] vnos) {
        for (int ii = 1; ii < vnos.Length; ++ii) {
            if (vnos[ii].VN != vnos[0].VN)
                throw new Exception($"Cannot join VNOperations across different VNStates: {vnos[0].VN}, {vnos[ii].VN}");
        }
    }

    public VNOperation And(params VNOperation[] nxt) => Parallel(nxt.Prepend(this).ToArray());

    public VNOperation Then(params VNOperation[] nxt) {
        CheckUniformity(nxt);
        if (VN != nxt[0].VN)
            throw new Exception($"Cannot join VNOperations across different VNStates: {VN}, {nxt[0].VN}");
        return new VNOperation(VN, nxt.Prepend(this).SelectMany(vno => vno.Suboperations).ToArray()) {
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

    public TaskAwaiter<Completion> GetAwaiter() => Task.GetAwaiter();

    /// <summary>
    /// Create a copy of this awaitable that can be cancelled by the provided cancellation token.
    /// </summary>
    public VNOperation BoundCT(ICancellee cT) => this with {
        Suboperations = Suboperations.Select(f => (Func<VNOpTracker, Task>)(o => f(o.BoundCT(cT)))).ToArray()
    };
    ILazyAwaitable<Completion> ILazyAwaitable<Completion>.BoundCT(ICancellee cT) => BoundCT(cT);
}
}