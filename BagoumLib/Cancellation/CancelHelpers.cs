using System;
using BagoumLib.Cancellation;
using JetBrains.Annotations;

namespace BagoumLib.Cancellation {
[PublicAPI]
public static class CancelHelpers {
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