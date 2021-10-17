using System;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Cancellation {

/// <summary>
/// A token passed into tasks to track cancellation.
/// Similar to CancellationToken, but allocates minimal garbage, does not require disposal,
/// and *is not thread-safe*.
/// </summary>
[PublicAPI]
public interface ICancellee {
    /// <summary>
    /// Used to mark the significance of cancellation client-side.
    /// A value LEQ 0 (default 0) indicates that the operation has not been cancelled.
    /// </summary>
    int CancelLevel { get; }
    bool Cancelled { get; } // => CancelLevel > 0
    /// <summary>
    /// Get the youngest ancestor cancellee that is not a passthrough.
    /// </summary>
    ICancellee Root { get; }
}

[PublicAPI]
public class Cancellable : ICancellee {
    public static readonly ICancellee Null = new Cancellable();
    public int CancelLevel { get; private set; }
    public bool Cancelled => CancelLevel > 0;
    public ICancellee Root => this;
    public void Cancel(int level) => CancelLevel = Math.Max(level, CancelLevel);
    public void Cancel() => Cancel(CancelHelpers.HardCancelLevel);
    public void SoftCancel() => Cancel(CancelHelpers.SoftSkipLevel);
}

/// <summary>
/// Dependency situations where X constructs Y constructs Z may be organized such that
/// Y has a PassthroughCancellee of (X, Y) and Z has a PassthroughCancellee of (X, Z).
/// In this case, the formation of Z's JointCancellee needs to skip Y, treating X as the root
/// against which to make a joint. 
/// </summary>
[PublicAPI]
public class PassthroughCancellee : ICancellee {
    public readonly ICancellee root;
    private readonly ICancellee local;
    public int CancelLevel => Math.Max(root.CancelLevel, local.CancelLevel);
    public bool Cancelled => CancelLevel > 0;
    public ICancellee Root => root.Root;

    public PassthroughCancellee(ICancellee? root, ICancellee? local) {
        this.root = root ?? Cancellable.Null;
        this.local = local ?? Cancellable.Null;
    }

}

[PublicAPI]
public class JointCancellee : ICancellee {
    private readonly ICancellee c1;
    private readonly ICancellee c2;
    public int CancelLevel => Math.Max(c1.CancelLevel, c2.CancelLevel);
    public bool Cancelled => CancelLevel > 0;
    public ICancellee Root => this;

    public JointCancellee(ICancellee? c1, ICancellee? c2) {
        this.c1 = c1 ?? Cancellable.Null;
        this.c2 = c2 ?? Cancellable.Null;
    }

    public JointCancellee(ICancellee? c1, out Cancellable token) {
        this.c1 = c1 ?? Cancellable.Null;
        this.c2 = token = new Cancellable();
    }

}

/// <summary>
/// A token passed into tasks to track cancellation.
/// Similar to ICancellee, but when it is cancelled, it must be provided a value of type T
/// (for example, a successor task to run).
/// </summary>
[PublicAPI]
public interface ICancellee<T> {
    bool Cancelled(out T value);
}

[PublicAPI]
public class GCancellable<T> : ICancellee<T> {
    public static readonly ICancellee<T> Null = new GCancellable<T>();
    private Maybe<T> obj = Maybe<T>.None;

    public bool Cancelled(out T value) => obj.Try(out value);

    public void Cancel(T value) {
        obj = Maybe<T>.Of(value);
    }
}

}