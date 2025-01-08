using System;
using System.Collections;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using JetBrains.Annotations;

namespace BagoumLib.Tasks;

/// <summary>
/// Helpers for awaiting tasks.
/// </summary>
[PublicAPI]
public static class WaitingUtils {
    /// <summary>
    /// An Action that does nothing.
    /// </summary>
    public static readonly Action NoOp = () => { };
    
    /// <summary>
    /// Get an action that completes a task.
    /// </summary>
    public static Action GetAwaiter(out Task t) {
        var tcs = new TaskCompletionSource<bool>();
        t = tcs.Task;
        return () => tcs.SetResult(true);
    }
    
    /// <summary>
    /// Get an action that completes a task.
    /// </summary>
    public static Action GetUnitAwaiter(out Task<Unit> t) {
        var tcs = new TaskCompletionSource<Unit>();
        t = tcs.Task;
        return () => tcs.SetResult(default);
    }

    /// <summary>
    /// Get an action that must be called `ct` times to complete a task.
    /// </summary>
    public static Action GetManyAwaiter(int ct, out Task t) {
        var tcs = new TaskCompletionSource<bool>();
        t = tcs.Task;
        var acc = 0;
        return () => {
            if (++acc == ct)
                tcs.SetResult(true);
        };
    }

    /// <summary>
    /// Get an action that completes a task.
    /// </summary>
    public static Action<Completion> GetCompletionAwaiter(out Task<Completion> t) {
        var tcs = new TaskCompletionSource<Completion>();
        t = tcs.Task;
        return c => {
            if (c == Completion.Cancelled)
                tcs.SetCanceled();
            else
                tcs.SetResult(c);
        };
    }
    
    /// <summary>
    /// Get an action that sets a boolean value (returned by `cond`) to true.
    /// </summary>
    public static Action GetCondition(out Func<bool> cond) {
        bool completed = false;
        cond = () => completed;
        return () => completed = true;
    }

    /// <summary>
    /// Get a callback that must be called `ct` times in order to invoke `whenAll`.
    /// </summary>
    public static Action GetManyCallback(int ct, Action whenAll) {
        if (ct == 1) return whenAll;
        int acc = 0;
        return () => {
            if (++acc == ct) 
                whenAll();
        };
    }
    

    /// <summary>
    /// Waits for the given amount of time, but can be cancelled early by the cT.
    /// </summary>
    public static IEnumerator WaitFor(float time, Action<Completion> done, ICancellee cT, Func<float> dT) {
        for (float elapsed = 0; elapsed < time; elapsed += dT()) {
            if (cT.Cancelled) break;
            yield return null;
        }
        done(cT.ToCompletion());
    }
    
    /// <summary>
    /// Waits until the condition is satisfied, but can be cancelled early by the cT.
    /// </summary>
    public static IEnumerator WaitFor(Func<bool> condition, Action<Completion> done, ICancellee cT) {
        while (!condition()) {
            if (cT.Cancelled) break;
            yield return null;
        }
        done(cT.ToCompletion());
    }
    

    /// <summary>
    /// Waits until the cT is cancelled.
    /// </summary>
    public static IEnumerator Spin(Action<Completion> done, ICancellee cT) {
        while (true) {
            if (cT.Cancelled) break;
            yield return null;
        }
        done(cT.ToCompletion());
    }
}