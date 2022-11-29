using System;
using System.Collections;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using JetBrains.Annotations;

namespace BagoumLib.Tasks {
[PublicAPI]
public static class WaitingUtils {
    
    public static Action GetAwaiter(out Task t) {
        var tcs = new TaskCompletionSource<bool>();
        t = tcs.Task;
        return () => tcs.SetResult(true);
    }

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
    
    public static Action GetAwaiter(out Func<bool> t) {
        bool done = false;
        t = () => done;
        return () => done = true;
    }
    
    public static Action GetCondition(out Func<bool> t) {
        bool completed = false;
        t = () => completed;
        return () => completed = true;
    }
    public static Action GetManyCondition(int ct, out Func<bool> t) {
        int acc = 0;
        t = () => acc == ct;
        return () => ++acc;
    }
    public static Action GetManyCallback(int ct, Action whenAll) {
        if (ct == 1) return whenAll;
        int acc = 0;
        return () => {
            if (++acc == ct) whenAll();
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
}