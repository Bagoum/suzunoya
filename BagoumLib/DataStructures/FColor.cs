using System;
using System.Drawing;
using System.Numerics;
using BagoumLib.Mathematics;

namespace BagoumLib.DataStructures {
/// <summary>
/// Representation of RGBA colors with 0-1 float values for each field.
/// <br/>(Fields may exceed the 0-1 range.)
/// </summary>
public struct FColor {
    public float r;
    public float g;
    public float b;
    public float a;

    public FColor(float r, float g, float b, float a = 1) {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    public FColor WithA(float newA) => new(r, g, b, newA);

    public static FColor Lerp(FColor a, FColor b, float t) => LerpU(a, b, BMath.Clamp(0, 1, t));
    public static FColor LerpU(FColor a, FColor b, float t) {
        return new FColor(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
    }

    public override string ToString() => $"RGBA({r:F3}, {g:F3}, {b:F3}, {a:F3})";
    public override int GetHashCode() => ((Vector4) this).GetHashCode();
    public override bool Equals(object? other) => other is FColor c && this == c;

    public static FColor operator +(FColor x, FColor y) => new(x.r + y.r, x.g + y.g, x.b + y.b, x.a + y.a);
    public static FColor operator -(FColor x, FColor y) => new(x.r - y.r, x.g - y.g, x.b - y.b, x.a - y.a);
    public static FColor operator *(FColor x, FColor y) => new(x.r * y.r, x.g * y.g, x.b * y.b, x.a * y.a);
    public static FColor operator *(FColor x, float f) => new(x.r * f, x.g * f, x.b * f, x.a * f);
    public static FColor operator *(float f, FColor x) => new(x.r * f, x.g * f, x.b * f, x.a * f);
    public static FColor operator /(FColor x, float f) => new(x.r / f, x.g / f, x.b / f, x.a / f);
    public static bool operator ==(FColor x, FColor y) => (Vector4) x == (Vector4) y;
    public static bool operator !=(FColor x, FColor y) => !(x == y);

    public static implicit operator Vector4(FColor c) => new(c.r, c.g, c.b, c.a);
    public static implicit operator FColor(Vector4 v) => new(v.X, v.Y, v.Z, v.W);

    public static implicit operator FColor(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    public static FColor White => new FColor(1, 1, 1, 1);
    public static FColor Clear => new FColor(0, 0, 0, 0);
}

}