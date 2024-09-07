using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Expressions;
using JetBrains.Annotations;
using InternalTaskWhenAll = System.Func<System.Threading.Tasks.Task[], System.Threading.Tasks.Task>;

namespace BagoumLib.Tasks {
/// <summary>
/// Task-related helpers.
/// </summary>
[PublicAPI]
public static class Utilities {
    /// <summary>
    /// Calls Task.InternalWhenAll, skipping array reallocation.
    /// <br/>DO NOT pass null tasks to this!
    /// </summary>
    public static readonly InternalTaskWhenAll TaskWhenAll = (InternalTaskWhenAll)
        Delegate.CreateDelegate(typeof(InternalTaskWhenAll),
            ExFunction.Wrap<Task>("InternalWhenAll", typeof(Task[])).Mi);
    
    /// <summary>
    /// Return a task that is completed when both provided tasks are completed.
    /// </summary>
    public static Task And(this Task? t1, Task? t2) {
        if (t1?.IsCompletedSuccessfully ?? true)
            return t2 ?? Task.CompletedTask;
        if (t2?.IsCompletedSuccessfully ?? true)
            return t1;
        return TaskWhenAll([t1, t2]);
    }

    /// <summary>
    /// Run two tasks in sequence.
    /// </summary>
    public static async Task Then(this Task? t1, Func<Task?> t2) {
        if (t1 != null)
            await t1;
        if (t2() is { } t)
            await t;
    }

    /// <summary>
    /// Return a Func that calls the provided action and then returns a null task.
    /// </summary>
    public static Func<T, Task?> AsTask<T>(this Action a) => _ => {
        a();
        return null;
    };

    /// <inheritdoc cref="AsTask{T}(System.Action)"/>
    public static Func<T, Task?> AsTask<T>(this Action<T> a) => x => {
        a(x);
        return null;
    };

    /// <summary>
    /// Runs the map function on all items in the list and returns a Task.WhenAll.
    /// </summary>
    public static Task All<T>(this IReadOnlyList<T> items, Func<T, Task?> map) {
        if (items.Count == 0) return Task.CompletedTask;
        if (items.Count == 1) return map(items[0]) ?? Task.CompletedTask;
        var tasks = new Task[items.Count];
        for (int ii = 0; ii < items.Count; ++ii)
            tasks[ii] = map(items[ii]) ?? Task.CompletedTask;
        return TaskWhenAll(tasks);
    }

    /// <summary>
    /// Removes completed or null tasks from the list, and then calls WhenAll.
    /// </summary>
    public static Task All(this List<Task?> tasks) {
        for (int ii = tasks.Count - 1; ii >= 0; --ii) {
            if (tasks[ii]?.IsCompletedSuccessfully is not false)
                tasks.RemoveAt(ii);
        }
        return tasks.Count switch {
            0 => Task.CompletedTask,
            1 => tasks[0]!,
            _ => Task.WhenAll(tasks)
        };
    }

    /// <summary>
    /// Removes completed or null tasks from the list, and then calls WhenAll.
    /// </summary>
    public static Task All(this IEnumerable<Task?> tasks) {
        var lis = new List<Task>();
        foreach (var t in tasks) {
            if (t?.IsCompletedSuccessfully is false)
                lis.Add(t);
        }
        return lis.Count switch {
            0 => Task.CompletedTask,
            1 => lis[0]!,
            _ => Task.WhenAll(lis)
        };
    }

    /// <summary>
    /// Runs a continuation after a task and logs errors from the task or the continuation.
    /// <br/>The continuation runs even if the task is cancelled/hits an exception.
    /// <br/>If the continuation hits an error,
    ///  the task result will contain that error (with the original task error nested within it).
    /// <br/>It is useful to use this on unawaited tasks with a null continuation, as it logs errors.
    /// </summary>
    public static Task ContinueWithSync(this Task t, Action? done = null) {
        if (t.IsCompleted) {
            //don't directly throw here. store the exception in the task result
            // so an exception is only thrown if the task is awaited.
            if (FinalizeContinueWith(t, done) is { } exc)
                return Task.FromException(exc);
            return t;
        } else
            return _ContinueWithSync(t, done);
    }

    private static async Task _ContinueWithSync(this Task t, Action? done = null) {
        //This implementation is faster than using ContinueWith(done, TaskContinuationOptions.ExecuteSynchronously)
        // in Unity due to Unity synchronization context overhead.
        try {
            await t;
        } finally {
            if (FinalizeContinueWith(t, done) is { } exc)
                throw exc;
        }
    }

    private static Exception? FinalizeContinueWith(Task t, Action? done) {
        Exception? exc = t.Exception;
        try {
            done?.Invoke();
        } catch (Exception e) {
            exc = new Exception(e.Message, exc);
        }
        if (exc != null)
            Logging.Logs.Error(exc, 
                "Exceptions occured within a task continuation. " +
                "If this continuation is awaited by the main thread, then this error may be repeated.");
        return exc;
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
                Logging.Logs.Error(exc, 
                    "Exceptions occured within a task continuation. " +
                    "If this continuation is awaited by the main thread, then this error may be repeated.");
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
            if (t.IsCanceled) 
                tcs.SetCanceled();
            else if (t.IsFaulted) 
                tcs.SetException(t.Exception ?? new Exception("Unknown task failure"));
            else
                tcs.SetResult(t.Result);
        }
    }
    /// <summary>
    /// Put the result of this task into a <see cref="TaskCompletionSource{Unit}"/>.
    /// </summary>
    public static async Task Pipe(this Task t, TaskCompletionSource<Unit> tcs) {
        try {
            await t;
        } finally {
            if (t.IsCanceled) 
                tcs.SetCanceled();
            else if (t.IsFaulted) 
                tcs.SetException(t.Exception ?? new Exception("Unknown task failure"));
            else
                tcs.SetResult(default);
        }
    }
}
}