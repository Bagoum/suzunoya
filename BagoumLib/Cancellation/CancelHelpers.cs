using System;
using BagoumLib.Cancellation;
using JetBrains.Annotations;

namespace BagoumLib.Cancellation {
[PublicAPI]
public static class CancelHelpers {
    public static bool IsHardCancelled(this ICancellee c) => c.Cancelled && c.CancelLevel >= ICancellee.HardCancelLevel;
    
    public static bool IsSoftCancelled(this ICancellee c) => c.Cancelled && c.CancelLevel < ICancellee.HardCancelLevel;

    public static Completion ToCompletion(this ICancellee c) => c.CancelLevel switch {
        <= 0 => Completion.Standard,
        { } when c.CancelLevel < ICancellee.HardCancelLevel => Completion.SoftSkip,
        _ => Completion.Cancelled
    };

    public static void ThrowIfHardCancelled(this ICancellee c) {
        if (c.IsHardCancelled()) throw new OperationCanceledException();
    }
    
    public static void ThrowIfCancelled(this ICancellee c) {
        if (c.Cancelled) throw new OperationCanceledException();
    }

    public static Action Guard(this ICancellee c, Action ifNotCancelled) => () => {
        if (!c.Cancelled) ifNotCancelled();
    };

    public static Action<T> Guard<T>(this ICancellee c, Action<T> ifNotCancelled) => x => {
        if (!c.Cancelled) ifNotCancelled(x);
    };
}
}