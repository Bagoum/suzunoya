using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace BagoumLib.Mathematics {
public static class BMath {
    internal const float HPI = (float)Math.PI * 0.5f;
    internal const float PI = (float)Math.PI;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Mod(double by, double x) => x - by * Math.Floor(x / by);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Mod(float by, float x) => x - by * (float) Math.Floor(x / by);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mod(int by, int x) {
        x %= by;
        return (x < 0) ? x + by : x;
    }

    public static float SoftMod(float by, float x) {
        float vd = Mod(2 * by, x);
        return vd > by ? 
            (2 * by - vd) : 
            vd;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int low, int high, int x) => 
        x < low ? low 
        : x > high ? high 
        : x;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float low, float high, float x) => 
        x < low ? low 
        : x > high ? high 
        : x;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LerpU(float a, float b, float t) => a * (1 - t) + b * t;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => LerpU(a, b, Clamp(0, 1, t));

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