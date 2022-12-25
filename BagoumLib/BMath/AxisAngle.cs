using System;
using System.Numerics;
using JetBrains.Annotations;

namespace BagoumLib.Mathematics {
/// <summary>
/// Representation of a rotation around an axis by an angle (in radians).
/// <br/>Interoperable with <see cref="Quaternion"/>.
/// </summary>
[PublicAPI]
public readonly struct AxisAngle {
    /// <summary>
    /// Axis of rotation (unit vector).
    /// </summary>
    public readonly Vector3 Axis;
    /// <summary>
    /// Radians of rotation.
    /// </summary>
    public readonly float Angle;

    /// <summary>
    /// Create an <see cref="AxisAngle"/>.
    /// </summary>
    /// <param name="axis">Axis of rotation. Must be normalized.</param>
    /// <param name="angle">Radians of rotation.</param>
    public AxisAngle(Vector3 axis, float angle) {
        Axis = axis;
        Angle = angle;
    }

    /// <summary>
    /// Rotate a 3D vector by this AxisAngle.
    /// </summary>
    public Vector3 Rotate(in Vector3 v) {
        //https://en.wikipedia.org/wiki/Rodrigues%27_rotation_formula
        var cos = MathF.Cos(Angle);
        return v * cos + Vector3.Cross(Axis, v) * MathF.Sin(Angle) + Axis * (Vector3.Dot(Axis, v) * (1 - cos));
    }
    
    /// <summary>
    /// Multiply the angle of rotation in an AxisAngle by a multiplier.
    /// </summary>
    public static AxisAngle operator *(float multiplier, in AxisAngle rot) => 
        new(rot.Axis, multiplier * rot.Angle);

    /// <summary>
    /// Sequence two axis-angle rotations. (Uses quaternions, since the direct calculation is excessively complex.)
    /// </summary>
    public static AxisAngle operator *(in AxisAngle rot2, in AxisAngle rot1) =>
        (Quaternion)rot2 * (Quaternion)rot1;

    /// <summary>
    /// Convert an AxisAngle to a half-angle Quaternion.
    /// </summary>
    public static implicit operator Quaternion(AxisAngle rot) => Quaternion.CreateFromAxisAngle(rot.Axis, rot.Angle);

    /// <summary>
    /// Convert a half-angle Quaternion to an AxisAngle, representing the rotation as positive. If the rotation is zero,
    ///  then assume the axis is UnitZ.
    /// </summary>
    public static implicit operator AxisAngle(Quaternion q) {
        var halfAngle = MathF.Acos(q.W);
        var sin = MathF.Sqrt(1 - q.W * q.W); //=MathF.Sin(angle);
        if (sin < 1e-8f)
            //halfAngle is ~0 or ~180, both of which map to an angle of 0
            return new(Vector3.UnitZ, 0);
        return new(new Vector3(q.X / sin, q.Y / sin, q.Z / sin), 2 * halfAngle);
    }
}
}