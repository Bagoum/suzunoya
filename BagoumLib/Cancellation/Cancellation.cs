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
    /// When a supported process (such as tweening) is cancelled, it will set the final value and exit without
    /// throwing an exception by default. However, if the cancel level is GEQ
    /// this value, it will not set a final value and instead throw OperationCancelledException.
    /// Make sure this is greater than SoftSkipLevel.
    /// <br/>Note that consumers are not required to distinguish skip levels.
    /// </summary>
    public static int HardCancelLevel { get; set; } = 2;
    /// <summary>
    /// Cancelling with this value on a supported process will set the final value and exit without
    /// throwing an exception.
    /// Make sure this is greater than zero and less than HardCancelLevel.
    /// <br/>Note that cancelling with this value may result in the continuation of the process. It is a "recommendation" to skip.
    /// <br/>Note that consumers are not required to distinguish skip levels.
    /// </summary>
    public static int SoftSkipLevel { get; set; } = 1;
    
    /// <summary>
    /// Used to mark the significance of cancellation client-side.
    /// A value LEQ 0 (default 0) indicates that the operation has not been cancelled.
    /// </summary>
    int CancelLevel { get; }
    
    /// <summary>
    /// True iff the token is cancelled (<see cref="CancelLevel"/> > 0).
    /// </summary>
    bool Cancelled => CancelLevel > 0;
    /// <summary>
    /// Get the youngest ancestor cancellee that is not a passthrough.
    /// </summary>
    ICancellee Root => this;
}


/// <summary>
/// A source of cancellation information.
/// Similar to CancellationTokenSource, but allocates minimal garbage, does not require disposal,
/// and *is not thread-safe*.
/// <br/>NB: This implements IDisposable, but does not generally require disposal;
///  Dispose is an alias for Cancel.
/// </summary>
[PublicAPI]
public interface ICancellable : ICancellee, IDisposable {
    /// <summary>
    /// Mark a cancellable as cancelled.
    /// </summary>
    /// <param name="level">Numerical level of cancellation. 0 = no cancellation.</param>
    public void Cancel(int level);
    
    /// <summary>
    /// Cancel with a level of HardCancelLevel.
    /// </summary>
    public void Cancel() => Cancel(ICancellee.HardCancelLevel);
    
    /// <summary>
    /// Cancel with a level of SoftSkipLevel.
    /// </summary>
    public void SoftCancel() => Cancel(ICancellee.SoftSkipLevel);
    
    void IDisposable.Dispose() => Cancel();
    
    /// <summary>
    /// Get a token that reflects the cancellation status of this cancellable.
    /// </summary>
    public ICancellee Token { get; }

}


/// <inheritdoc cref="ICancellable"/>
[PublicAPI]
public class Cancellable : ICancellable {
    /// <summary>
    /// A cancellee that is never cancelled.
    /// </summary>
    public static readonly ICancellee Null = new Cancellable();
    /// <inheritdoc/>
    public int CancelLevel { get; private set; }
    /// <inheritdoc/>
    public bool Cancelled => CancelLevel > 0;
    /// <inheritdoc/>
    public void Cancel(int level) => CancelLevel = Math.Max(level, CancelLevel);

    //i love traits
    /// <inheritdoc/>
    public void Cancel() => Cancel(ICancellee.HardCancelLevel);
    /// <inheritdoc/>
    public void SoftCancel() => Cancel(ICancellee.SoftSkipLevel);
    
    /// <inheritdoc/>
    public ICancellee Token => this;

    /// <summary>
    /// Cancel a token, and then replace it with a new one.
    /// </summary>
    public static ICancellee Replace(ref Cancellable? cT) {
        cT?.Cancel();
        return cT = new();
    }

    /// <summary>
    /// Return a <see cref="MinCancellee"/> that is cancelled when both
    ///  of the source cancellees are cancelled.
    /// </summary>
    public static ICancellee Extend(ICancellee cT, ICancellee? other) {
        if (other?.Cancelled is not false)
            return cT;
        if (cT.Cancelled)
            return other;
        return new MinCancellee(cT, other);
    }
}

/// <summary>
/// A cancellable token that is cancelled when its parent is cancelled or it is locally cancelled (via <see cref="Cancel(int)"/>).
/// <br/>Note that `new JointCancellable(parent)` is almost the same as `new JointCancellee(parent, new Cancellable())`,
///  but this is slightly more garbage-efficient.
/// </summary>
[PublicAPI]
public class JointCancellable : ICancellable {
    private int localCancelLevel;
    /// <summary>
    /// The parent cancellation token. When it is cancelled, this token will also be cancelled.
    /// </summary>
    public ICancellee Parent { get; }
    /// <inheritdoc/>
    public int CancelLevel => Math.Max(Parent.CancelLevel, localCancelLevel);
    /// <inheritdoc/>
    public bool Cancelled => CancelLevel > 0;
    
    /// <summary>
    /// True if Cancel was called on this token, regardless of whether or not
    /// the parent token is cancelled.
    /// </summary>
    public bool LocallyCancelled => localCancelLevel > 0;

    /// <summary>
    /// Create a <see cref="JointCancellable"/>.
    /// </summary>
    /// <param name="parent">Parent token. If the parent token is cancelled, then this is also cancelled.</param>
    public JointCancellable(ICancellee parent) {
        this.Parent = parent;
    }
    
    /// <inheritdoc/>
    public void Cancel(int level) => localCancelLevel = Math.Max(level, localCancelLevel);

    //i love traits
    /// <inheritdoc/>
    public void Cancel() => Cancel(ICancellee.HardCancelLevel);
    /// <inheritdoc/>
    public void SoftCancel() => Cancel(ICancellee.SoftSkipLevel);
    
    /// <inheritdoc/>
    public ICancellee Token => this;
}

/// <summary>
/// Dependency situations where X constructs Y constructs Z may be organized such that
/// Y has a PassthroughCancellee of (X, Y) and Z has a PassthroughCancellee of (X, Z).
/// In this case, the formation of Z's JointCancellee needs to skip Y, treating X as the root
/// against which to make a joint. 
/// </summary>
[PublicAPI]
public class PassthroughCancellee : ICancellee {
    private readonly ICancellee root;
    private readonly ICancellee local;
    /// <inheritdoc/>
    public int CancelLevel => Math.Max(root.CancelLevel, local.CancelLevel);
    /// <inheritdoc/>
    public bool Cancelled => CancelLevel > 0;
    /// <inheritdoc/>
    public ICancellee Root => root.Root;

    public PassthroughCancellee(ICancellee? root, ICancellee? local) {
        this.root = root ?? Cancellable.Null;
        this.local = local ?? Cancellable.Null;
    }

}

/// <summary>
/// A cancellee that is cancelled when either of its two parents are cancelled.
/// </summary>
[PublicAPI]
public class JointCancellee : ICancellee {
    private readonly ICancellee c1;
    private readonly ICancellee c2;
    /// <inheritdoc/>
    public int CancelLevel => Math.Max(c1.CancelLevel, c2.CancelLevel);
    /// <inheritdoc/>
    public bool Cancelled => CancelLevel > 0;

    public JointCancellee(ICancellee? c1, ICancellee? c2) {
        this.c1 = c1 ?? Cancellable.Null;
        this.c2 = c2 ?? Cancellable.Null;
    }

    public JointCancellee(ICancellee? c1, out Cancellable token) {
        this.c1 = c1 ?? Cancellable.Null;
        this.c2 = token = new Cancellable();
    }

    /// <summary>
    /// Create a cancellee from two parents. If either is null, the other will be directly returned.
    /// </summary>
    public static ICancellee From(ICancellee? c1, ICancellee? c2) {
        if (c1 == null) return c2 ?? Cancellable.Null;
        if (c2 == null) return c1;
        return new JointCancellee(c1, c2);
    }
}

/// <summary>
/// A cancel token that is cancelled when both of its parents are cancelled.
/// </summary>
[PublicAPI]
public class MinCancellee(ICancellee c1, ICancellee c2) : ICancellee {
    /// <inheritdoc/>
    public int CancelLevel => Math.Min(c1.CancelLevel, c2.CancelLevel);

    /// <summary>
    /// Create a cancellee from two parents. If either is null, the other will be directly returned.
    /// </summary>
    public static ICancellee From(ICancellee? c1, ICancellee? c2) {
        if (c1 is null) return c2 ?? Cancellable.Null;
        if (c2 is null) return c1;
        return new MinCancellee(c1, c2);
    }
}

/// <summary>
/// A cancellee that proxies a source by treating soft cancellations as no-cancellation.
/// </summary>
[PublicAPI]
public record StrongCancellee(ICancellee Source) : ICancellee {
    /// <inheritdoc/>
    public int CancelLevel => Source.CancelLevel >= ICancellee.HardCancelLevel ? ICancellee.HardCancelLevel : 0;
}

/// <summary>
/// A token passed into tasks to track cancellation.
/// Similar to <see cref="ICancellee"/>, but when it is cancelled, it must be provided a value of type T
/// (for example, a canceller for a state machine passing the next state).
/// </summary>
[PublicAPI]
public interface ICancellee<T> : ICancellee {
    /// <summary>
    /// Check if the token is cancelled, and if it is, get the cancellation value.
    /// </summary>
    new bool Cancelled(out T value);
}

/// <inheritdoc cref="ICancellee{T}"/>
[PublicAPI]
public class GCancellable<T> : ICancellee<T> {
    /// <summary>
    /// A cancellee that is never cancelled.
    /// </summary>
    public static readonly ICancellee<T> Null = new GCancellable<T>();
    /// <inheritdoc/>
    public int CancelLevel { get; private set; }
    private Maybe<T> obj = Maybe<T>.None;

    
    /// <inheritdoc/>
    public bool Cancelled(out T value) => obj.Try(out value);

    /// <summary>
    /// Cancel the token with the provided value.
    /// </summary>
    public void Cancel(int level, T value) {
        if (level >= CancelLevel) {
            CancelLevel = level;
            obj = Maybe<T>.Of(value);
        }
    }

    /// <summary>
    /// Cancel the token with the provided value, setting the cancel level to <see cref="ICancellee.HardCancelLevel"/>.
    /// </summary>
    public void Cancel(T value) => Cancel(ICancellee.HardCancelLevel, value);
}

}