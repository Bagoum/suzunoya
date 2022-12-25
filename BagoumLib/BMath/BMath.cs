using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace BagoumLib.Mathematics {
/// <summary>
/// Static class providing low-level math utilities.
/// </summary>
[PublicAPI]
public static class BMath {
    /// <summary>
    /// Half of pi.
    /// </summary>
    public const float HPI = (float)Math.PI * 0.5f;
    /// <summary>
    /// Pi.
    /// </summary>
    public const float PI = (float)Math.PI;
    /// <summary>
    /// Tau (2*pi).
    /// </summary>
    public const float TAU = 2f * PI;
    /// <summary>
    /// 2*tau (4*pi).
    /// </summary>
    public const float TWAU = 4f * PI;
    /// <summary>
    /// Phi (golden ratio, ~1.618)
    /// </summary>
    public const float PHI = 1.6180339887498948482045868343656381f;
    /// <summary>
    /// Inverse of phi (~.618)
    /// </summary>
    public const float IPHI = PHI - 1f;
    /// <summary>
    /// pi/180: multiply by this to convert degrees to radians.
    /// </summary>
    public const float degRad = PI / 180f;
    /// <summary>
    /// 180/pi: multiply by this to convert radians to degrees.
    /// </summary>
    public const float radDeg = 180f / PI;
    
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
        return x < 0 ? x + by : x;
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
            2 * by - vd : 
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
        var t1 = t > src ? t - mod : t + mod;
        return Math.Abs(src - t) < Math.Abs(src - t1) ? t : t1;
    }

    /// <summary>
    /// Rotate a vector by a quaternion. Note that the quaternion should already have its angle halved.
    /// Generating a quaternion via <see cref="Quaternion.CreateFromAxisAngle"/> will automatically halve the axis.
    /// <br/>Equal to rotator * Quaternion(vec, 0) * rotator.Conjugate()
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Rotate(in this Vector3 vec, in Quaternion rotator) {
        Quaternion qv;
        qv.X = vec.X;
        qv.Y = vec.Y;
        qv.Z = vec.Z;
        qv.W = 0;
        Quaternion rotConj;
        rotConj.X = -rotator.X;
        rotConj.Y = -rotator.Y;
        rotConj.Z = -rotator.Z;
        rotConj.W = rotator.W;
        var result = rotator * qv * rotConj;
        return new(result.X, result.Y, result.Z);
    }
    
    /// <summary>
    /// Slerp between two quaternions via the closest path between them. Same as Quaternion.Slerp.
    /// </summary>
    public static Quaternion SlerpNear(in Quaternion q1, in Quaternion q2, float t) => Quaternion.Slerp(q1, q2, t);

    //From Quaternion
    private const float SlerpEpsilon = 1e-6f;
    
    /// <summary>
    /// Slerp between two quaternions via the farthest path between them.
    /// </summary>
    public static Quaternion SlerpFar(in Quaternion q1, in Quaternion q2, float t) {
        //Code based on C# implementation of Quaternion.Slerp
        
        float cosOmega = q1.X * q2.X + q1.Y * q2.Y +
                         q1.Z * q2.Z + q1.W * q2.W;

        bool flip = false;

        // THE ONLY DIFFERENCE between SlerpNear and SlerpFar is this check; for SlerpNear it is cosOmega < 0
        if (cosOmega > 0.0f) {
            flip = true;
            cosOmega = -cosOmega;
        }

        float s1, s2;

        if (cosOmega > 1.0f - SlerpEpsilon) {
            // Too close, do straight linear interpolation.
            s1 = 1.0f - t;
            s2 = flip ? -t : t;
        } else {
            float omega = MathF.Acos(cosOmega);
            float invSinOmega = 1 / MathF.Sin(omega);

            s1 = MathF.Sin((1.0f - t) * omega) * invSinOmega;
            s2 = flip
                ? -MathF.Sin(t * omega) * invSinOmega
                : MathF.Sin(t * omega) * invSinOmega;
        }

        Quaternion ans;

        ans.X = s1 * q1.X + s2 * q2.X;
        ans.Y = s1 * q1.Y + s2 * q2.Y;
        ans.Z = s1 * q1.Z + s2 * q2.Z;
        ans.W = s1 * q1.W + s2 * q2.W;

        return ans;
    }

    /// <summary>
    /// Slerp between two quaternions.
    /// <br/>Note that this is not the same as Quaternion.Slerp, which always takes the shortest path.
    /// </summary>
    public static Quaternion Slerp(in Quaternion q1, in Quaternion q2, float t) {
        float dot = q1.X * q2.X + q1.Y * q2.Y + q1.Z * q2.Z + q1.W * q2.W;
        float s1, s2;

        if (dot > 0.9995f) {
            // Too close, do straight linear interpolation.
            s1 = 1.0f - t;
            s2 = t;
        } else {
            float omega = MathF.Acos(dot);
            float invSinOmega = 1 / MathF.Sin(omega);

            s1 = MathF.Sin((1.0f - t) * omega) * invSinOmega;
            s2 = MathF.Sin(t * omega) * invSinOmega;
        }

        Quaternion ans;

        ans.X = s1 * q1.X + s2 * q2.X;
        ans.Y = s1 * q1.Y + s2 * q2.Y;
        ans.Z = s1 * q1.Z + s2 * q2.Z;
        ans.W = s1 * q1.W + s2 * q2.W;

        return ans;
    }

    /// <summary>
    /// Check if two quaternions are (approximately) equal using their dot product.
    /// </summary>
    public static bool EqualsByDot(this in Quaternion q1, in Quaternion q2) =>
        Quaternion.Dot(q1, q2) > 0.9999989867f;

    /// <summary>
    /// Convert a half-angled quaternion to euler angles (in degrees).
    /// </summary>
    public static Vector3 ToEulersD(this in Quaternion q1) => new(
        radDeg * MathF.Atan2(2 * (q1.W * q1.X + q1.Y * q1.Z), q1.W * q1.W + q1.Z * q1.Z - q1.X * q1.X - q1.Y * q1.Y),
        radDeg * MathF.Asin(2 * (q1.W * q1.Y - q1.X * q1.Z)),
        radDeg * MathF.Atan2(2 * (q1.W * q1.Z + q1.X * q1.Y), q1.W * q1.W + q1.X * q1.X - q1.Y * q1.Y - q1.Z * q1.Z));

    /// <summary>
    /// Convert euler angles (in degrees) to a half-angled quaternion, with order ZXY.
    /// </summary>
    public static Quaternion ToQuaternionD(this in Vector3 rot) =>
        Quaternion.CreateFromYawPitchRoll(rot.Y * degRad, rot.X * degRad, rot.Z * degRad);

    /// <summary>
    /// Create a half-angled quaternion to rotate a given number of degrees around an axis.
    /// </summary>
    public static Quaternion RotateAroundD(this Vector3 axis, float rotDeg) =>
        Quaternion.CreateFromAxisAngle(axis, rotDeg * degRad);

    /// <summary>
    /// Create a half-angled quaternion to rotate a given number of degrees around the Z-axis.
    /// </summary>
    public static Quaternion RotateAroundZ(this float rotDeg) => Vector3.UnitZ.RotateAroundD(rotDeg);
}
}