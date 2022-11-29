using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace BagoumLib.Tasks {
/// <summary>
/// Task-related helpers.
/// </summary>
[PublicAPI]
public static class Utilities {
    /// <summary>
    /// Runs a continuation after a task and logs errors from the task or the continuation.
    /// <br/>The continuation runs even if the task is cancelled/hits an exception.
    /// <br/>It is useful to use this on unawaited tasks with a null continuation, as it logs errors.
    /// </summary>
    public static async Task ContinueWithSync(this Task t, Action? done = null) {
        //This implementation is faster than using ContinueWith(done, TaskContinuationOptions.ExecuteSynchronously)
        // in Unity due to Unity synchronization context overhead.
        try {
            await t;
        } finally {
            Exception? exc = t.Exception;
            try {
                done?.Invoke();
            } catch (Exception e) {
                exc = new Exception(e.Message, exc);
            }
            if (exc != null) {
                Logging.Log(LogMessage.Error(exc, 
                    "Exceptions occured within a task continuation. " +
                    "If this continuation is awaited by the main thread, then this error may be repeated."));
                throw exc;
            }
        }
    }

    /// <summary>
    /// Runs a continuation and logs errors from the task or the continuation.
    /// <br/>Note that this only executes the continuation if the task completes successfully.
    /// </summary>
    public static async Task ContinueSuccessWithSync<T>(this Task<T> t, Action<T>? done = null) {
        try {
            await t;
        } finally {
            Exception? exc = t.Exception;
            try {
                if (t.IsCompletedSuccessfully)
                    done?.Invoke(t.Result);
            } catch (Exception e) {
                exc = new Exception(e.Message, exc);
            }
            if (exc != null) {
                Logging.Log(LogMessage.Error(exc, 
                    "Exceptions occured within a task continuation. " +
                    "If this continuation is awaited by the main thread, then this error may be repeated."));
                throw exc;
            }
        }
    }

    /// <summary>
    /// Put the result of this task into a <see cref="TaskCompletionSource{T}"/>.
    /// </summary>
    public static async Task Pipe<T>(this Task<T> t, TaskCompletionSource<T> tcs) {
        try {
            await t;
        } finally {
            if (t.IsCanceled) tcs.SetCanceled();
            else if (t.IsFaulted) tcs.SetException(t.Exception ?? new Exception("Unknown task failure"));
            else
                tcs.SetResult(t.Result);
        }
    }
}
}