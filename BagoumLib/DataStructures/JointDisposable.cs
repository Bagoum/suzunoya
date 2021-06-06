using System;

namespace BagoumLib.DataStructures {
public class JointDisposable : IDisposable {
    private readonly IDisposable[] disps;
    private readonly Action? afterDispose;
    public JointDisposable(Action? afterDispose, params IDisposable[] disps) {
        this.disps = disps;
        this.afterDispose = afterDispose;
    }

    public void Dispose() {
        for (int ii = 0; ii < disps.Length; ++ii)
            disps[ii].Dispose();
        afterDispose?.Invoke();
    }
}
}