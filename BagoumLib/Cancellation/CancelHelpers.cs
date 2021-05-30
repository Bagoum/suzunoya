using System;
using BagoumLib.Cancellation;
using JetBrains.Annotations;

namespace BagoumLib.Cancellation {
[PublicAPI]
public static class CancelHelpers {
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

    public static bool IsHardCancelled(this ICancellee c) => c.Cancelled && c.CancelLevel >= HardCancelLevel;
    
    public static bool IsSoftCancelled(this ICancellee c) => c.Cancelled && c.CancelLevel < HardCancelLevel;

    public static Completion ToCompletion(this ICancellee c) => c.CancelLevel switch {
        <= 0 => Completion.Standard,
        { } when c.CancelLevel < HardCancelLevel => Completion.SoftSkip,
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