using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// A disposable token that, when disposed, disposes multiple other tokens and then calls a function.
/// </summary>
[PublicAPI]
public class JointDisposable : IDisposable {
    private readonly IReadOnlyList<IDisposable?> disps;
    private readonly Action? afterDispose;
    private bool disposed = false;
    
    /// <inheritdoc cref="JointDisposable"/>
    public JointDisposable(Action? afterDispose, params IDisposable?[] disps) {
        this.disps = disps;
        this.afterDispose = afterDispose;
    }
    /// <inheritdoc cref="JointDisposable"/>
    public JointDisposable(Action? afterDispose, IReadOnlyList<IDisposable?> disps) {
        this.disps = disps;
        this.afterDispose = afterDispose;
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (disposed) return;
        disposed = true;
        for (int ii = 0; ii < disps.Count; ++ii)
            disps[ii]?.Dispose();
        afterDispose?.Invoke();
    }

    /// <summary>
    /// Create a <see cref="JointDisposable"/> if `b` is nonnull. Otherwise, return `a`.
    /// </summary>
    public static IDisposable MaybeJoint(IDisposable a, IDisposable? b) {
        if (b is null) return a;
        return new JointDisposable(null, a, b);
    }
}
}