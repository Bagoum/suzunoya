using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using static BagoumLib.Mathematics.BMath;

namespace BagoumLib.Mathematics {
/// <summary>
/// A continuous function over the domain [0, 1] with f(0) = 0 and f(1) = 1.
/// Behavior is undefined if called with a value outside [0, 1].
/// </summary>
/// <param name="x">Control value in range [0, 1]</param>
public delegate float Easer(float x);
/// <summary>
/// A repository of easing functions (see the <see cref="Easer"/> delegate for definition).
/// All easing functions are prefixed with "E".
/// Some functions may also have their paired derivatives present. These are prefixed with "DE".
/// Some functions may construct easing functions given a parameter, such as the power easer. These are prefixed with "CE".
/// </summary>
[PublicAPI]
public static class Easers {
    
    /// <summary>
    /// Sine easer (slow at beginning).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EInSine(float x) => 1f - (float) Math.Cos(HPI * x);
    /// <summary>
    /// Sine easer (slow at end).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EOutSine(float x) => (float) Math.Sin(HPI * x);
    /// <summary>
    /// Sine easer (slow at beginning and end).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EIOSine(float x) => 0.5f - 0.5f * (float) Math.Cos(PI * x);

    /// <summary>
    /// Quadratic easer (slow at beginning).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EInQuad(float x) => x * x;
    /// <summary>
    /// Quadratic easer (slow at end).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EOutQuad(float x) => 1f - (1 - x) * (1 - x);
    /// <summary>
    /// Quadratic easer (slow at beginning and end).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EIOQuad(float x) =>
        (x *= 2f) < 1f ?
            0.5f * x * x :
            1 - 0.5f * (x - 2) * (x - 2);

    /// <summary>
    /// Quartic easer (slow at beginning).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EinQuart(float x) => x * x * x * x;
    /// <summary>
    /// Quartic easer (slow at end).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EOutQuart(float x) => 1 - --x * x * x * x;
    /// <summary>
    /// Quartic easer (slow at beginning and end).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EIOQuart(float x) =>
        (x *= 2f) < 1f ?
            0.5f * x * x * x * x :
            1 - 0.5f * (x -= 2) * x * x * x;

    public const float BackElasticity = 1.7f;
    public const float IOBackElasticity = BackElasticity * 1.53f;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EInBack(float x) => x * x * ((BackElasticity + 1) * x - BackElasticity);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CEOutBack(float elast, float x) => 1 + --x * x * ((elast + 1) * x + elast);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EOutBack(float x) => 1 + --x * x * ((BackElasticity + 1) * x + BackElasticity);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EIOBack(float x) =>
        (x *= 2f) < 1f ?
            0.5f * x * x * ((IOBackElasticity + 1) * x - IOBackElasticity) :
            1 - 0.5f * (x -= 2) * x * ((-1 - IOBackElasticity) * x - IOBackElasticity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EInElastic(float x) => 
        (float)(-Math.Pow(2, 10 * x - 10) * Math.Sin((x - 1.075) * Math.PI / 0.15));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EOutElastic(float x) => 
        (float)(1+Math.Pow(2, -10 * x) * Math.Sin((x - .075) * Math.PI / 0.15));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EIOElastic(float x) =>
        (x *= 2f) < 1f ?
            (float) (-Math.Pow(2, 10 * x - 11) * Math.Sin((x - 1.1) * Math.PI / 0.2)) :
            (float) (1 - Math.Pow(2, 9 - 10 * x) * Math.Sin((1.9 - x) * Math.PI / 0.2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EInBounce(float x) => 1 - EOutBounce(1 - x);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EOutBounce(float x) {
        //Expanded form of CEOutBounce for points [-1/3, 1/3, 2/3, 5.3/6, 1]
        if (x < 1 / 3f)
            return 9 * x * x;
        if (x < 2 / 3f)
            return 9 * (x - 0.5f) * (x - 0.5f) + 0.75f;
        if (x < 5.3f / 6)
            return 9 * (x - 4.65f / 6) * (x - 4.65f / 6) + .894375f;
        return 9 * (x - 5.65f / 6) * (x - 5.65f / 6) + .969375f;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EIOBounce(float x) =>
        (x *= 2f) < 1f ?
            0.5f - 0.5f * EOutBounce(1 - x) :
            0.5f + 0.5f * EOutBounce(x - 1);

    /// <summary>
    /// f(x) = x. Same as EIdentity.
    /// </summary>
    public static float ELinear(float x) => x;
    /// <summary>
    /// f(x) = x. Same as ELinear.
    /// </summary>
    public static float EIdentity(float x) => x;

    public static Easer CEOutPow(float pow) => x => 1f - (float) Math.Pow(1 - x, pow);
    
    /// <summary>
    /// </summary>
    /// <param name="bounces">Points at which to create zeroes in the bouncing.
    /// 0th element must be negative of 1st element.
    /// Final element must be 1.
    /// Eg: [-1/3, 1/3, 2/3, 5.3/6, 1]</param>
    /// <returns></returns>
    public static Easer CEOutBounce(params float[] bounces) {
        var amp = 1 / (bounces[1] * bounces[1]);
        return x => {
            for (int ii = 1; ii < bounces.Length; ++ii) {
                var b = bounces[ii];
                if (x < b) {
                    var avg = (b + bounces[ii - 1]) / 2;
                    return 1 - amp * ((avg - b) * (avg - b) - (x - avg) * (x - avg));
                }
            }
            return 1;
        };
    }
}

/// <summary>
/// Functions that are similar to easers, but don't satisfy all qualifications (eg. they end at 0 instead of 1).
/// </summary>
[PublicAPI]
public static class OffEasers {
    /// <summary>
    /// Sine wave that goes through half a period in the range [0, 1], starting and ending at 0.
    /// </summary>
    public static float ESine010(float x) => (float) Math.Sin(Math.PI * x);
    
    /// <summary>
    /// Triangle wave that has F(0) = 0, F(0.5) = 1, F(1) = 0.
    /// </summary>
    public static float ESoftmod010(float x) => 2 * SoftMod(0.5f, x);
}
}