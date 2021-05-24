using System;
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
    
    public static float EInSine(float x) => 1f - (float) Math.Cos(HPI * x);
    
    public static float EOutSine(float x) => (float) Math.Sin(HPI * x);
    public static float DEOutSine(float x) => HPI * (float) Math.Cos(HPI * x);
    
    public static float EIOSine(float x) => 0.5f - 0.5f * (float) Math.Cos(PI * x);
    
    public static float EOutQuad(float x) => 1f - (float)Math.Pow(1 - x, 4);

    public static float ELinear(float x) => x;
    public static float EIdentity(float x) => x;

    public static Easer CEOutPow(float pow) => x => 1f - (float) Math.Pow(1 - x, pow);


}
}