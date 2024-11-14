namespace BagoumLib.Mathematics;

/// <summary>
/// A position description composed of a nonrotational offset
/// and a rotational offset.
/// </summary>
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
    public static V2RV2 Zero => V2RV2.NRot(0, 0);
    public (float x, float y) ComputedLocation {
        get {
            var (x, y) = BMath.RotateVectorDeg(rx, ry, angle);
            return (nx + x, ny + y);
        }
    }

    public V2RV2(float nx, float ny, float rx, float ry, float angle_deg) {
        this.nx = nx;
        this.ny = ny;
        this.rx = rx;
        this.ry = ry;
        this.angle = angle_deg;
    }

    public static V2RV2 NRot(float nx, float ny) => new V2RV2(nx, ny, 0, 0, 0);
    public static V2RV2 NRotAngled(float nx, float ny, float angle) => new V2RV2(nx, ny, 0, 0, angle);
    public static V2RV2 Rot(float rx, float ry, float angle=0f) => new V2RV2(0,0,rx,ry,angle);
    public static V2RV2 RX(float rx, float angle=0f) => new V2RV2(0,0,rx,0,angle);
    public static V2RV2 RY(float ry, float angle=0f) => new V2RV2(0,0,0,ry,angle);
    public static V2RV2 Angle(float angle) => new V2RV2(0, 0, 0, 0, angle);


    public V2RV2 Bank(float? new_angle_deg=null) {
        var (tlx, tly) = ComputedLocation;
        return new V2RV2(tlx, tly, 0, 0, new_angle_deg ?? angle);
    }

    public V2RV2 BankOffset(float angle_offset_deg) => Bank(angle + angle_offset_deg);
    
    public V2RV2 ForceAngle(float new_ang) => new V2RV2(nx, ny, rx, ry, new_ang);
    
    public static V2RV2 operator +(V2RV2 a, V2RV2 b) {
        return new V2RV2(a.nx + b.nx, a.ny + b.ny, a.rx + b.rx, a.ry + b.ry, a.angle + b.angle);
    }
    public static V2RV2 operator -(V2RV2 a, V2RV2 b) {
        return new V2RV2(a.nx - b.nx, a.ny - b.ny, a.rx - b.rx, a.ry - b.ry, a.angle - b.angle);
    }
    public static V2RV2 operator *(float f, V2RV2 a) {
        return new V2RV2(f*a.nx, f*a.ny, f*a.rx, f*a.ry, f*a.angle);
    }
    public static V2RV2 operator /(V2RV2 a, float f) {
        return new V2RV2(a.nx/f, a.ny/f, a.rx/f, a.ry/f, a.angle/f);
    }
    public static V2RV2 operator +(V2RV2 a, float ang_deg) {
        return new V2RV2(a.nx, a.ny, a.rx, a.ry, a.angle + ang_deg);
    }
    /// <inheritdoc/>
    public override string ToString() {
        return $"<{(decimal) nx},{(decimal) ny}:{(decimal) rx},{(decimal) ry}:{(decimal) angle}>";
    }
}
