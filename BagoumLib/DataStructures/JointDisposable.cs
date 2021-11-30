using System;

namespace BagoumLib.DataStructures {
public class JointDisposable : IDisposable {
    private readonly IDisposable[] disps;
    private readonly Action? afterDispose;
    private bool disposed = false;
    public JointDisposable(Action? afterDispose, params IDisposable[] disps) {
        this.disps = disps;
        this.afterDispose = afterDispose;
    }

    public void Dispose() {
        if (disposed) return;
        disposed = true;
        for (int ii = 0; ii < disps.Length; ++ii)
            disps[ii].Dispose();
        afterDispose?.Invoke();
    }
}
}