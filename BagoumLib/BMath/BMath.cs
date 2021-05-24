using System;
using System.Runtime.CompilerServices;

namespace BagoumLib.Mathematics {
public static class BMath {
    internal const float HPI = (float)Math.PI * 0.5f;
    internal const float PI = (float)Math.PI;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Mod(double by, double x) => x - by * Math.Floor(x / by);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mod(int by, int x) {
        x %= by;
        return (x < 0) ? x + by : x;
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
    
}
}