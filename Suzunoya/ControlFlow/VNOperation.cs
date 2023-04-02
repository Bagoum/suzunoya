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
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {
/// <summary>
/// A lazy wrapper around a task that is not started until it is awaited.
/// This allows chaining in ways that are a bit difficult for tasks.
/// </summary>
public interface ILazyAwaitable {
    /// <summary>
    /// Task object (accessing this property will cause it to be computed).
    /// </summary>
    Task Task { get; }
    /// <summary>
    /// Syntactic sugar for `await Task`.
    /// </summary>
    TaskAwaiter GetAwaiter() => Task.GetAwaiter();
    //ILazyAwaitable BoundCT(ICancellee cT);

    /// <summary>
    /// Awaitable that does nothing.
    /// </summary>
    public static readonly ILazyAwaitable Null = new LazyAction(() => { });
}

/// <summary>
/// <see cref="ILazyAwaitable"/> with a specified task return type.
/// </summary>
public interface ILazyAwaitable<T> : ILazyAwaitable {
    Task ILazyAwaitable.Task => Task;
    //ILazyAwaitable ILazyAwaitable.BoundCT(ICancellee cT) => BoundCT(cT);

    /// <inheritdoc cref="ILazyAwaitable.Task"/>
    new Task<T> Task { get; }
    
    /// <inheritdoc cref="ILazyAwaitable.GetAwaiter"/>
    new TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();
    //new ILazyAwaitable<T> BoundCT(ICancellee cT);
}

/// <summary>
/// An function pretending to be a task.
/// </summary>
public record LazyFunc<T>(Func<T> lazyFunc) : ILazyAwaitable<T> {
    private Task<T>? loadedTask;
    /// <inheritdoc/>
    public Task<T> Task => loadedTask ??= LazyTask();

    private Task<T> LazyTask() {
        var result = lazyFunc();
        return System.Threading.Tasks.Task.FromResult(result);
    }

    /*public ILazyAwaitable<T> BoundCT(ICancellee cT) => new LazyFunc<T>(() => {
        if (cT.IsHardCancelled()) throw new OperationCanceledException();
        return lazyFunc();
    });*/

    /// <inheritdoc/>
    public TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();

    /// <summary>
    /// Implicit constructor for <see cref="LazyFunc{T}"/>.
    /// </summary>
    public static implicit operator LazyFunc<T>(Func<T> op) => new(op);
    
    /// <summary>
    /// Convert this to a <see cref="VNOperation"/> so it can be sequenced with VNOperation functions.
    /// </summary>
    public VNOperation AsVnOp(IVNState vn) => new(vn, _ => Task);
}

/// <summary>
/// An action predenting to be a task.
/// </summary>
public record LazyAction : LazyFunc<Unit> {
    /// <inheritdoc cref="LazyAction"/>
    public LazyAction(Action lazyOp) : base(() => {
        lazyOp();
        return Unit.Default;
    }) { }
    
    /// <inheritdoc cref="LazyAction"/>
    public static implicit operator LazyAction(Action op) => new(op);
}

/// <summary>
/// A set of lazily-computed tasks run in parallel.
/// </summary>
public record ParallelLazyAwaitable(params ILazyAwaitable[] tasks) : ILazyAwaitable {
    private Task? loadedTask;
    /// <inheritdoc/>
    public Task Task => loadedTask ??= Task.WhenAll(tasks.Select(t => t.Task));
    //public ILazyAwaitable BoundCT(ICancellee cT) => new ParallelLazyAwaitable(tasks.Select(t => t.BoundCT(cT)).ToArray());
}

/// <summary>
/// A set of lazily-computed tasks run in sequence.
/// </summary>
public record SequentialLazyAwaitable(params ILazyAwaitable?[] tasks) : ILazyAwaitable {
    private Task? loadedTask;
    /// <inheritdoc/>
    public Task Task => loadedTask ??= LazyTask();

    //public ILazyAwaitable BoundCT(ICancellee cT) => new SequentialLazyAwaitable(tasks.Select(t => t?.BoundCT(cT)).ToArray());
    
    private async Task LazyTask() {
        foreach (var t in tasks)
            if (t != null)
                await t;
    }
}

/// <summary>
/// A group of processes (generally <see cref="VNOperation"/>)
/// running on a VN bounded by a common cancellation/confirmation/interruption interface.
/// </summary>
public class VNProcessGroup : ICancellee {
    /// <summary>
    /// The process layer on which this process group is running.
    /// </summary>
    public VNInterruptionLayer ProcessLayer { get; }
    /// <summary>
    /// The <see cref="IVNState"/> on which this process group is running.
    /// </summary>
    public IVNState VN => ProcessLayer.VN;
    /// <summary>
    /// The last interruption that occured on this process group.
    /// <br/>NB: This is not set to null when the interruption is complete.
    /// </summary>
    public IVNInterruptionToken? LastInterruption { get; set; } = null;
    /// <summary>
    /// The cancellation token source bounding execution of this process group.
    /// </summary>
    public Cancellable OperationCTS { get; } = new();
    /// <summary>
    /// Whether or not the user can skip this process group.
    /// </summary>
    public bool userSkipAllowed;
    /// <summary>
    /// The cancellation token bounding execution of this process group.
    /// </summary>
    public ICancellee OperationCToken { get; }
    private (Cancellable cTs, IConfirmationReceiver recv)? confirmToken;
    
    /// The canceller governing when a confirm input is provided by the player.
    public ICancellee? ConfirmToken => confirmToken?.cTs;
    /// <summary>
    /// The object receiving confirmation.
    /// </summary>
    public IConfirmationReceiver? ConfirmReceiver => confirmToken?.recv;
    /// <summary>
    /// The number of <see cref="VNOperation"/>s dependent on this process group.
    /// </summary>
    public int OperationCTokenDependencies { get; private set; } = 0;
    /// <inheritdoc/>
    public int CancelLevel => Math.Max(OperationCToken.CancelLevel, ProcessLayer.InducedOperationCancelLevel);
    /// <inheritdoc/>
    public ICancellee Root => this;
        
    /// <inheritdoc cref="VNProcessGroup"/>
    public VNProcessGroup(VNInterruptionLayer ih, bool allowUserSkip) {
        ProcessLayer = ih;
        userSkipAllowed = allowUserSkip;
        OperationCToken = new JointCancellee(ih.VN.CToken, OperationCTS);
    }

    /// <summary>
    /// Indicate that a user-confirmation is required for at least one of the operations in this process group.
    /// </summary>
    public ICancellee AwaitConfirm() {
        return (confirmToken ??= (new Cancellable(), VN)).cTs;
    }

    /// <summary>
    /// Send a user-confirmation to all operations dependent on this process group.
    /// </summary>
    public void DoConfirm() {
        if (confirmToken?.cTs is { Cancelled: false }) {
            confirmToken?.cTs.Cancel(ICancellee.SoftSkipLevel);
            confirmToken = null;
        }
    }

    /// <summary>
    /// Get a token that indicates that a <see cref="VNOperation"/> is dependent on confirmation from this process group.
    /// </summary>
    public IDisposable CreateOpTracker() => new OperationTracker(this);
    private class OperationTracker : IDisposable {
        private readonly VNProcessGroup op;
        public OperationTracker(VNProcessGroup op) {
            this.op = op;
            ++op.OperationCTokenDependencies;
        }

        public void Dispose() {
            --op.OperationCTokenDependencies;
        }
    }

    
}

/// <summary>
/// The task that is produced when waiting for a confirmation signal.
/// This is similar to VNOperation, but cannot be soft-skipped except by interruption.
/// </summary>
public record VNConfirmTask(IVNState VN, VNOperation? Preceding, Func<VNProcessGroup, Task<Completion>> Confirm) : ILazyAwaitable<Completion> {
    private Task<Completion>? loadedTask;
    /// <inheritdoc/>
    public Task<Completion> Task => loadedTask ??= _AsTask();

    private async Task<Completion> _AsTask() {
        using var _ = VN.GetOperationCanceller(out var op);
        if (Preceding != null)
            await Preceding.TaskInGroup(op);
        return await Confirm(op);
    }
    
    /// <inheritdoc/>
    [UsedImplicitly]
    public TaskAwaiter<Completion> GetAwaiter() => Task.GetAwaiter();
    
}

/// <summary>
/// A VNOperation is a sequential sequence of tasks bounded by a common cancellation/interruption interface.
/// <br/>The cancellation/interruption is provided through <see cref="VNProcessGroup"/>.
/// If multiple VNOperations are run at the same time, they may end up sharing the same <see cref="VNProcessGroup"/>.
/// <br/>Tasks batched under a VNOperation do not need to check cancellation at their start or end.
/// </summary>
public record VNOperation(IVNState VN, params Func<VNCancellee, Task>[] Suboperations) : ILazyAwaitable<Completion> {
    /// <summary>
    /// The VN on which this operation is running.
    /// </summary>
    public IVNState VN { get; } = VN;
    /// <summary>
    /// The (sequential) tasks that compose this operation.
    /// </summary>
    public Func<VNCancellee, Task>[] Suboperations { get; init; } = Suboperations;

    /// <summary>
    /// Whether or not this operation can be soft-skipped by user input.
    /// </summary>
    public bool AllowUserSkip { get; init; } = true;

    private Task<Completion>? loadedTask;
    
    /// <inheritdoc/>
    public Task<Completion> Task => loadedTask ??= _AsTask();
    /// <summary>
    /// Create a task for running this operation on a specific <see cref="VNProcessGroup"/>.
    /// </summary>
    public Task<Completion> TaskInGroup(VNProcessGroup op) => loadedTask ??= _AsTask(op);
    /// <summary>
    /// Create a task for running this operation with an extra cancellation token.
    /// </summary>
    public Task<Completion> TaskWithCT(ICancellee cT) => loadedTask ??= _AsTask(cT);
    /// <summary>
    /// Create a <see cref="VNConfirmTask"/> that runs this task, and then wait for user confirmation.
    /// </summary>
    public VNConfirmTask C => VN.SpinUntilConfirm(this);

    private async Task<Completion> _AsTask(VNProcessGroup op, ICancellee? cT = null) {
        cT = new JointCancellee(op, cT);
        var tracker = new VNCancellee(VN, cT);
        tracker.ThrowIfHardCancelled();
        foreach (var t in Suboperations) {
            await t(tracker);
            tracker.ThrowIfHardCancelled();
        }
        if (op.ProcessLayer.Status == InterruptionStatus.Interrupted) {
            VN.Run(WaitingUtils.WaitFor(() => op.ProcessLayer.Status != InterruptionStatus.Interrupted,
                //don't respect soft-skip during interrupt hanging
                WaitingUtils.GetCompletionAwaiter(out var aw), new StrongCancellee(tracker)));
            await aw;
            tracker.ThrowIfHardCancelled();
        }
        return tracker.ToCompletion();
    }

    private async Task<Completion> _AsTask(ICancellee? cT = null) {
        using var _ = VN.GetOperationCanceller(out var op, AllowUserSkip);
        return await _AsTask(op, cT);
    }

    /// <summary>
    /// Create a <see cref="VNOperation"/> that first runs this task, and then the provided actions.
    /// </summary>
    public VNOperation Then(Action nxt) {
        var nsubops = Suboperations.ToArray();
        var ft = nsubops[^1];
        nsubops[^1] = ct => ft(ct).ContinueWithSync(nxt);
        return this with {Suboperations = nsubops};
    }
    
    /// <summary>
    /// Create a <see cref="VNOperation"/> that first runs this task, and then the provided tasks in sequence.
    /// </summary>
    public VNOperation Then(params Func<ICancellee, Task>[] nxt) =>
        this with {Suboperations = Suboperations.Concat(nxt).ToArray()};

    private static void CheckUniformity(VNOperation[] vnos) {
        for (int ii = 1; ii < vnos.Length; ++ii) {
            if (vnos[ii].VN != vnos[0].VN)
                throw new Exception($"Cannot join VNOperations across different VNStates: {vnos[0].VN}, {vnos[ii].VN}");
        }
    }

    /// <summary>
    /// Create a <see cref="VNOperation"/> that runs this task and the provided tasks in parallel.
    /// </summary>
    public VNOperation And(params VNOperation[] nxt) => Parallel(nxt.Prepend(this).ToArray());

    /// <summary>
    /// Create a <see cref="VNOperation"/> that first runs this task, and then the provided tasks in sequence.
    /// </summary>
    public VNOperation Then(params VNOperation[] nxt) {
        CheckUniformity(nxt);
        if (VN != nxt[0].VN)
            throw new Exception($"Cannot join VNOperations across different VNStates: {VN}, {nxt[0].VN}");
        return new VNOperation(VN, nxt.Prepend(this).SelectMany(vno => vno.Suboperations).ToArray()) {
            AllowUserSkip = nxt.All(v => v.AllowUserSkip)
        };
    }
    
    /// <summary>
    /// Create a <see cref="VNOperation"/> that runs the provided tasks in parallel.
    /// </summary>
    public static VNOperation Parallel(params VNOperation[] vnos) {
        CheckUniformity(vnos);
        return new VNOperation(vnos[0].VN, Parallel(vnos.Select(vno => Sequential(vno.Suboperations)))) {
            AllowUserSkip = vnos.All(v => v.AllowUserSkip)
        };
    }

    /// <summary>
    /// Create a <see cref="VNOperation"/> that runs the provided tasks in parallel.
    /// </summary>
    public static Func<T, Task> Parallel<T>(IEnumerable<Func<T, Task>> tasks) =>
        x => System.Threading.Tasks.Task.WhenAll(tasks.Select(t => t(x)));

    /// <summary>
    /// Create a <see cref="VNOperation"/> that runs the provided tasks in sequence.
    /// </summary>
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

    /// <inheritdoc/>
    public override string ToString() => $"VNOp (Len: {Suboperations.Length})";

    /// <inheritdoc/>
    public TaskAwaiter<Completion> GetAwaiter() => Task.GetAwaiter();
}
}