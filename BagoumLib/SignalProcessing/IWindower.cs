using System;

namespace BagoumLib.SignalProcessing;

/// <summary>
/// A windowing function.
/// </summary>
public interface IWindower {
    /// <summary>
    /// Return the 0-1 multiplier for index `i` out of a data array of length `N`.
    /// </summary>
    double WindowMultiplier(int i, int N);
}

/// <summary>
/// No windowing.
/// </summary>
public class NoWindow : IWindower {
    /// <inheritdoc/>
    public double WindowMultiplier(int i, int N) => 1;
}

/// <summary>
/// Hann windowing.
/// </summary>
public class HannWindow : IWindower {
    /// <inheritdoc/>
    public double WindowMultiplier(int i, int N) {
        var s = Math.Sin(Math.PI * i / (N - 1));
        return 2 * s * s;
    }
}