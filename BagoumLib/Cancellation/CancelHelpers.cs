using System;
using BagoumLib.Cancellation;
using JetBrains.Annotations;

namespace BagoumLib.Cancellation {
/// <summary>
/// Helpers for <see cref="ICancellee"/>.
/// </summary>
[PublicAPI]
public static class CancelHelpers {
    /// <summary>
    /// True iff the token's cancellation level is geq than <see cref="ICancellee.HardCancelLevel"/>.
    /// </summary>
    public static bool IsHardCancelled(this ICancellee c) => c.Cancelled && c.CancelLevel >= ICancellee.HardCancelLevel;
    
    /// <summary>
    /// True iff the token is cancelled, but not hard-cancelled (<see cref="IsHardCancelled"/>).
    /// </summary>
    public static bool IsSoftCancelled(this ICancellee c) => c.Cancelled && c.CancelLevel < ICancellee.HardCancelLevel;

    /// <summary>
    /// Convert the cancellation status of a token to <see cref="Completion"/> based on its cancel level.
    /// </summary>
    public static Completion ToCompletion(this ICancellee c) => c.CancelLevel switch {
        <= 0 => Completion.Standard,
        { } when c.CancelLevel < ICancellee.HardCancelLevel => Completion.SoftSkip,
        _ => Completion.Cancelled
    };

    /// <summary>
    /// Throw <see cref="OperationCanceledException"/> if the token is hard-cancelled (<see cref="IsHardCancelled"/>).
    /// </summary>
    public static void ThrowIfHardCancelled(this ICancellee c) {
        if (c.IsHardCancelled()) throw new OperationCanceledException();
    }
    
    /// <summary>
    /// Throw <see cref="OperationCanceledException"/> if the token is cancelled.
    /// </summary>
    public static void ThrowIfCancelled(this ICancellee c) {
        if (c.Cancelled) throw new OperationCanceledException();
    }

    /// <summary>
    /// Create an action that runs the provided action when the token is not cancelled.
    /// </summary>
    public static Action Guard(this ICancellee c, Action ifNotCancelled) => () => {
        if (!c.Cancelled) ifNotCancelled();
    };

    /// <inheritdoc cref="Guard"/>
    public static Action<T> Guard<T>(this ICancellee c, Action<T> ifNotCancelled) => x => {
        if (!c.Cancelled) ifNotCancelled(x);
    };
}
}