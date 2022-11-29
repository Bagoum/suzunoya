using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace BagoumLib.Mathematics {
/// <summary>
/// Static class providing low-level math utilities.
/// </summary>
public static class BMath {
    internal const float HPI = (float)Math.PI * 0.5f;
    internal const float PI = (float)Math.PI;
    
    /// <summary>
    /// Mod function. The result will always be zero or have the same sign as `by`.
    /// <br/>Note that a % n = Mod(n, a)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Mod(double by, double x) => x - by * Math.Floor(x / by);
    
    
    /// <inheritdoc cref="Mod(double, double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Mod(float by, float x) => x - by * (float) Math.Floor(x / by);

    /// <inheritdoc cref="Mod(double, double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mod(int by, int x) {
        x %= by;
        return (x < 0) ? x + by : x;
    }

    /// <summary>
    /// Triangle wave function. Has a period of 2*<paramref name="by"/>.
    /// </summary>
    /// <example>
    /// Softmod(4, 3) = 3
    /// <br/>Softmod(4, 4) = 4
    /// <br/>Softmod(4, 5) = 3
    /// <br/>Softmod(4, 8) = 0
    /// <br/>Softmod(4, 9) = 1
    /// </example>
    public static float SoftMod(float by, float x) {
        float vd = Mod(2 * by, x);
        return vd > by ? 
            (2 * by - vd) : 
            vd;
    }
    
    /// <summary>
    /// Clamp a value to the range [low, high].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int low, int high, int x) => 
        x < low ? low 
        : x > high ? high 
        : x;
    
    /// <inheritdoc cref="Clamp(int,int,int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float low, float high, float x) => 
        x < low ? low 
        : x > high ? high 
        : x;
    
    /// <summary>
    /// Lerp between a and b, but do not clamp t, so the result can be outside the range [a, b].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LerpU(float a, float b, float t) => a * (1 - t) + b * t;
    
    /// <summary>
    /// Lerp between a and b with t as a controller (clamped to [0, 1]).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => LerpU(a, b, Clamp(0, 1, t));

    /// <summary>
    /// Element-wise <see cref="Mod(float, float)"/>.
    /// </summary>
    public static Vector3 Mod(float by, Vector3 v) => new Vector3(Mod(by, v.X), Mod(by, v.Y), Mod(by, v.Z));

    /// <summary>
    /// Returns (target+n*mod) to minimize |src - (target+n*mod)|.
    /// </summary>
    public static float GetClosestAroundBound(float mod, float src, float target) {
        var t = (float)Math.Floor(src / mod) * mod + Mod(mod, target);
        var t1 = (t > src) ? (t - mod) : (t + mod);
        return Math.Abs(src - t) < Math.Abs(src - t1) ? t : t1;
    }
}
}