using System;
using System.Drawing;
using System.Numerics;
using BagoumLib.Mathematics;

namespace BagoumLib.DataStructures {
/// <summary>
/// Representation of RGBA colors with 0-1 float values for each field.
/// <br/>(Fields may exceed the 0-1 range.)
/// </summary>
public struct FColor : IEquatable<FColor> {
    /// <summary>
    /// Red component.
    /// </summary>
    public float r;
    /// <summary>
    /// Green component.
    /// </summary>
    public float g;
    /// <summary>
    /// Blue component.
    /// </summary>
    public float b;
    /// <summary>
    /// Alpha component.
    /// </summary>
    public float a;

    /// <summary>
    /// Create a new <see cref="FColor"/>.
    /// </summary>
    public FColor(float r, float g, float b, float a = 1) {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    /// <summary>
    /// Copy this color with a different alpha component.
    /// </summary>
    public FColor WithA(float newA) => new(r, g, b, newA);

    /// <summary>
    /// Lerp (clamped) between colors.
    /// </summary>
    public static FColor Lerp(FColor a, FColor b, float t) => LerpU(a, b, BMath.Clamp(0, 1, t));
    
    /// <summary>
    /// Lerp (unclamped) between colors.
    /// </summary>
    public static FColor LerpU(FColor a, FColor b, float t) {
        return new FColor(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
    }

    /// <inheritdoc/>
    public override string ToString() => $"RGBA({r:F3}, {g:F3}, {b:F3}, {a:F3})";
    
    /// <inheritdoc/>
    public override int GetHashCode() => ((Vector4) this).GetHashCode();
    
    /// <inheritdoc/>
    public override bool Equals(object? other) => other is FColor c && this == c;

    /// <summary>
    /// Add colors (unclamped).
    /// </summary>
    public static FColor operator +(FColor x, FColor y) => new(x.r + y.r, x.g + y.g, x.b + y.b, x.a + y.a);
    
    /// <summary>
    /// Subtract colors (unclamped).
    /// </summary>
    public static FColor operator -(FColor x, FColor y) => new(x.r - y.r, x.g - y.g, x.b - y.b, x.a - y.a);
    
    /// <summary>
    /// Multiply colors component-wise.
    /// </summary>
    public static FColor operator *(FColor x, FColor y) => new(x.r * y.r, x.g * y.g, x.b * y.b, x.a * y.a);
    
    /// <summary>
    /// Multiply all components of a color by a scalar (unclamped).
    /// </summary>
    public static FColor operator *(FColor x, float f) => new(x.r * f, x.g * f, x.b * f, x.a * f);

    /// <summary>
    /// Multiply all components of a color by a scalar (unclamped).
    /// </summary>
    public static FColor operator *(float f, FColor x) => new(x.r * f, x.g * f, x.b * f, x.a * f);
    
    /// <summary>
    /// Divide all components of a color by a scalar (unclamped).
    /// </summary>
    public static FColor operator /(FColor x, float f) => new(x.r / f, x.g / f, x.b / f, x.a / f);
    
    /// <summary>
    /// Check if two colors are equal.
    /// </summary>
    public static bool operator ==(FColor x, FColor y) => (Vector4) x == (Vector4) y;
    
    /// <summary>
    /// Check if two colors are not equal.
    /// </summary>
    public static bool operator !=(FColor x, FColor y) => !(x == y);

    /// <summary>
    /// Convert a color to a 0-1 vector4.
    /// </summary>
    public static implicit operator Vector4(FColor c) => new(c.r, c.g, c.b, c.a);
    /// <summary>
    /// Convert a 0-1 vector4 to a color.
    /// </summary>
    public static implicit operator FColor(Vector4 v) => new(v.X, v.Y, v.Z, v.W);

    /// <summary>
    /// Convert a 0-255 color to a 0-1 color.
    /// </summary>
    public static implicit operator FColor(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    /// <summary>
    /// White (1, 1, 1, 1).
    /// </summary>
    public static FColor White => new FColor(1, 1, 1, 1);
    /// <summary>
    /// Transparent (0, 0, 0, 0).
    /// </summary>
    public static FColor Clear => new FColor(0, 0, 0, 0);

    /// <inheritdoc/>
    public bool Equals(FColor other) => this == other;
}

}