using System;

namespace BagoumLib.DataStructures {
/// <summary>
/// A disposable token that, when disposed, disposes multiple other tokens and then calls a function.
/// </summary>
public class JointDisposable : IDisposable {
    private readonly IDisposable?[] disps;
    private readonly Action? afterDispose;
    private bool disposed = false;
    /// <summary>
    /// </summary>
    /// <param name="afterDispose">Function to call after all linked tokens are disposed</param>
    /// <param name="disps">Linked tokens to dispose when this token is disposed</param>
    public JointDisposable(Action? afterDispose, params IDisposable?[] disps) {
        this.disps = disps;
        this.afterDispose = afterDispose;
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (disposed) return;
        disposed = true;
        for (int ii = 0; ii < disps.Length; ++ii)
            disps[ii]?.Dispose();
        afterDispose?.Invoke();
    }
}
}