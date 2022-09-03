using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace BagoumLib.Tasks {
[PublicAPI]
public static class Utilities {
    private static Action<Task> WrapRethrow(Action? cb) => t => {
        Exception? exc = t.Exception;
        try {
            cb?.Invoke();
        } catch (Exception e) {
            exc = new Exception(e.Message, exc);
        }
        if (exc != null) {
            Logging.Log(LogMessage.Error(exc, 
                "Exceptions occured within a task continuation. " +
                "If this continuation is awaited by the main thread, then this error may be repeated below."));
            throw exc;
        }
    };

    /// <summary>
    /// Runs a continuation and logs errors from the task or the continuation.
    /// <br/>It is useful to use this on unawaited tasks, as it logs errors.
    /// </summary>
    public static Task ContinueWithSync(this Task t, Action? done = null) =>
        t.ContinueWith(WrapRethrow(done), TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>
    /// Returns true iff either t is equal to parent, or t is a strict subclass of parent.
    /// </summary>
    public static bool IsWeakSubclassOf(this Type? t, Type? parent) =>
        t == parent || (parent != null && t?.IsSubclassOf(parent) is true);
}
}