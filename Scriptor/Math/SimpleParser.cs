using System;
using BagoumLib;
using BagoumLib.Mathematics;
using static BagoumLib.Mathematics.BMath;

namespace Scriptor.Math;

/// <summary>
/// Helper methods for parsing simple values from strings.
/// </summary>
public static class SimpleParser {
    private const char decpt = '.';
    private const char zero = '0';
    private const char CPI = 'π';
    private const char CPHI = 'p';
    private const char CINVPHI = 'h';
    private const char CFRAME = 'f';
    private const char CFPS = 's';
    private const char C360H = 'c';
    
    /// <summary>
    /// Parse a float value. Supported shortcuts:
    /// <para>Up to two +- signs at the front</para>
    /// <para>Multiplier suffixes: p=phi, h=1/phi, f=1/120 (frame time), s=120 (fps)</para>
    /// <para>Effect suffixes: c = return 360h/x</para>
    /// </summary>
    public static float Float(string s) {
        if (TryFloat(s, out float f)) return f;
        throw new InvalidCastException($"Cannot convert \"{s}\" to float.");
    }
    
    /// <inheritdoc cref="Float(string)"/>
    public static bool TryFloat(string s, out float f) {
        return TryFloat(s, 0, s.Length, out f);
    }

    /// <inheritdoc cref="Float(string)"/>
    public static float Float(string s, int from, int to) {
        if (TryFloat(s, from, to, out float f)) return f;
        throw new InvalidCastException($"Cannot convert \"{s}\" to float.");
    }
    
    private static float NextFloat(string s, ref int from, ref int ii, char until) {
        while (++ii < s.Length) {
            if (s[ii] == until) {
                var f = SimpleParser.Float(s, from, ii);
                from = ii + 1;
                return f;
            }
        }
        throw new Exception("Couldn't find enough float values in the string.");
    }
    
    /// <summary>
    /// Parse a V2RV2 in the format &lt;nx;ny:rx;ry:angle&gt;.
    /// </summary>
    public static V2RV2 ParseV2RV2(string s) {
        // Format: <float;float:float;float:float> (nx,ny,rx,ry,angle)
        // OR the RV2 format (rx,ry,angle).
        if (s == "<>") return V2RV2.Zero;
        if (s.CountOf(':') == 0) return V2RV2.Angle(SimpleParser.Float(s, 1, s.Length - 1));
        if (s.CountOf(':') == 1) return ParseShortV2RV2(s);
        int ii = 0;
        int from = 1;
        var nx = NextFloat(s, ref from, ref ii, ';');
        var ny = NextFloat(s, ref from, ref ii, ':');
        var rx = NextFloat(s, ref from, ref ii, ';');
        var ry = NextFloat(s, ref from, ref ii, ':');
        return new V2RV2(nx, ny, rx, ry, SimpleParser.Float(s, from, s.Length - 1));
    }
    
    private static V2RV2 ParseShortV2RV2(string s) {
        // Format: <float;float:float> ; args are rx,ry,angle resp.
        int ii = 0;
        int from = 1;
        float x = 0;
        while (++ii < s.Length) {
            if (s[ii] == ';') {
                x = SimpleParser.Float(s, from, ii);
                from = ii + 1;
                break;
            }
        }
        while (++ii < s.Length) {
            if (s[ii] == ':') {
                float y = SimpleParser.Float(s, from, ii);
                from = ii + 1;
                return V2RV2.Rot(x, y, SimpleParser.Float(s, from, s.Length - 1));
            }
        }
        throw new FormatException("Bad V2RV2 formatting: " + s);
    }
    
    
    /// <inheritdoc cref="Float(string)"/>
    public static bool TryFloat(string s, int from, int to, out float f) {
        f = 0f;
        if (to == from) return true;
        if (to == from + 1 && s[from] == '_') {
            f = BMath.IntFloatMax;
            return true;
        }
        float dec_mult = 0.1f;
        float multiplier = 1f;
        bool foundDecimal = false;
        int ii = from;
        char first = s[from];
        int slen = s.Length;
        bool c360inv = false;
        //Allow --, +-, -+, ++ at front; these are parsed as signs.
        if (first == '-') {
            ++ii;
            if (ii >= slen) return false;
            if (s[ii] == '-') { 
                ++ii;
            } else {
                if (s[ii] == '+') ++ii;
                multiplier *= -1;
            }
        } else if (first == '+') {
            ++ii;
            if (ii >= slen) return false;
            if (s[ii] == '+') {
                ++ii;
            } else if (s[ii] == '-') {
                ++ii;
                multiplier *= -1;
            }
        }
        for (; ii < to; ++ii) {
            char c = s[ii];
            if (c == decpt) {
                foundDecimal = true;
            } else {
                int val = c - zero;
                if (val < 0 || val > 9) {
                    if (c == CPHI) {
                        multiplier *= PHI;
                    } else if (c == CINVPHI) {
                        multiplier *= IPHI;
                    } else if (c == CFRAME) {
                        multiplier *= 1f/120f;
                    } else if (c == CFPS) {
                        multiplier *= 120f;
                    } else if (c == CPI) {
                        multiplier *= PI;
                    } else if (c == C360H) {
                        c360inv = true;
                    } else return false;
                } else if (foundDecimal) {
                    f += dec_mult * val;
                    dec_mult *= 0.1f;
                } else {
                    f *= 10f;
                    f += val;
                }
            }
        }
        f *= multiplier;
        if (c360inv) f = 360f * IPHI / f;
        return true;
    }

}