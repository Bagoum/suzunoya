using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace BagoumLib.Tasks {
[PublicAPI]
public static class WaitingUtils {
    
    public static Action GetAwaiter(out Task t) {
        var tcs = new TaskCompletionSource<bool>();
        t = tcs.Task;
        return () => tcs.SetResult(true);
    }

    public static Action<Completion> GetCompletionAwaiter(out Task t) {
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
    
    public static Action<T> GetAwaiter<T>(out Task<T> t) {
        var tcs = new TaskCompletionSource<T>();
        t = tcs.Task;
        return f => tcs.SetResult(f);
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
        int acc = 0;
        return () => {
            if (++acc == ct) whenAll();
        };
    }
}
}