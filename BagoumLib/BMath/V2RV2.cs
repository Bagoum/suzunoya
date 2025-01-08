using JetBrains.Annotations;

namespace BagoumLib.Mathematics;

/// <summary>
/// A position description composed of a nonrotational offset
/// and a rotational offset.
/// </summary>
[PublicAPI]
public struct V2RV2 {
    /// <summary>
    /// X-component of nonrotational offset
    /// </summary>
    public float nx;
    /// <summary>
    /// Y-component of nonrotational offset
    /// </summary>
    public float ny;
    /// <summary>
    /// X-component of rotational offset
    /// </summary>
    public float rx;
    /// <summary>
    /// Y-component of rotational offset
    /// </summary>
    public float ry;
    /// <summary>
    /// Rotation (degrees) of rotational offset
    /// </summary>
    public float angle;
    
    /// <summary>
    /// Create a V2RV2 with all fields set to zero.
    /// </summary>
    public static V2RV2 Zero => new(0, 0, 0, 0, 0);
    
    /// <summary>
    /// Get the computed position of this V2RV2, calculated as (nx, ny) + Rotate((rx, ry), angle).
    /// </summary>
    public (float x, float y) ComputedLocation {
        get {
            var (x, y) = BMath.RotateVectorDeg(rx, ry, angle);
            return (nx + x, ny + y);
        }
    }

    /// <inheritdoc cref="V2RV2"/>
    public V2RV2(float nx, float ny, float rx, float ry, float angle_deg) {
        this.nx = nx;
        this.ny = ny;
        this.rx = rx;
        this.ry = ry;
        this.angle = angle_deg;
    }

    /// <summary>
    /// Create a V2RV2 with nonrotational coordinates and an angle of zero.
    /// </summary>
    public static V2RV2 NRot(float nx, float ny) => new V2RV2(nx, ny, 0, 0, 0);
    
    /// <summary>
    /// Create a V2RV2 with nonrotational coordinates.
    /// </summary>
    public static V2RV2 NRotAngled(float nx, float ny, float angle) => new V2RV2(nx, ny, 0, 0, angle);
    
    /// <summary>
    /// Create a V2RV2 with rotational coordinates.
    /// </summary>
    public static V2RV2 Rot(float rx, float ry, float angle=0f) => new V2RV2(0,0,rx,ry,angle);
    
    /// <summary>
    /// Create a V2RV2 with all fields set to zero except the rotational-X and angle.
    /// </summary>
    public static V2RV2 RX(float rx, float angle=0f) => new V2RV2(0,0,rx,0,angle);
    
    /// <summary>
    /// Create a V2RV2 with all fields set to zero except the rotational-Y and angle.
    /// </summary>
    public static V2RV2 RY(float ry, float angle=0f) => new V2RV2(0,0,0,ry,angle);
    
    /// <summary>
    /// Create a V2RV2 with all fields set to zero except the angle.
    /// </summary>
    public static V2RV2 Angle(float angle) => new V2RV2(0, 0, 0, 0, angle);

    /// <summary>
    /// Convert the V2RV2 into non-rotational coordinates only, and set the angle to the
    ///  override if provided or the current angle.
    /// </summary>
    public V2RV2 Bank(float? new_angle_deg=null) {
        var (tlx, tly) = ComputedLocation;
        return new V2RV2(tlx, tly, 0, 0, new_angle_deg ?? angle);
    }

    /// <summary>
    /// Convert the V2RV2 into non-rotational coordinates only, and set the angle to the
    ///  current angle plus the provided offset.
    /// </summary>
    public V2RV2 BankOffset(float angle_offset_deg) => Bank(angle + angle_offset_deg);
    
    /// <summary>
    /// Create a V2RV2 with the angle set to new_ang.
    /// </summary>
    public V2RV2 ForceAngle(float new_ang) => new V2RV2(nx, ny, rx, ry, new_ang);
    
    /// <summary>
    /// Find the sum of each field in two V2RV2s.
    /// </summary>
    public static V2RV2 operator +(V2RV2 a, V2RV2 b) {
        return new V2RV2(a.nx + b.nx, a.ny + b.ny, a.rx + b.rx, a.ry + b.ry, a.angle + b.angle);
    }
    
    /// <summary>
    /// Find the difference of each field in two V2RV2s.
    /// </summary>
    public static V2RV2 operator -(V2RV2 a, V2RV2 b) {
        return new V2RV2(a.nx - b.nx, a.ny - b.ny, a.rx - b.rx, a.ry - b.ry, a.angle - b.angle);
    }
    
    /// <summary>
    /// Multiply all elements of a V2RV2.
    /// </summary>
    public static V2RV2 operator *(float f, V2RV2 a) {
        return new V2RV2(f*a.nx, f*a.ny, f*a.rx, f*a.ry, f*a.angle);
    }

    /// <summary>
    /// Divide all elements of a V2RV2.
    /// </summary>
    public static V2RV2 operator /(V2RV2 a, float f) => (1 / f) * a;
    
    /// <summary>
    /// Increase the angle of a V2RV2.
    /// </summary>
    public static V2RV2 operator +(V2RV2 a, float ang_deg) {
        return new V2RV2(a.nx, a.ny, a.rx, a.ry, a.angle + ang_deg);
    }
    /// <inheritdoc/>
    public override string ToString() {
        return $"<{(decimal) nx},{(decimal) ny}:{(decimal) rx},{(decimal) ry}:{(decimal) angle}>";
    }
}
