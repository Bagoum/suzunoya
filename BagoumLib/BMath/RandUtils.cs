using System;
using JetBrains.Annotations;

namespace BagoumLib.Mathematics {
/// <summary>
/// Utilities for Random.
/// </summary>
[PublicAPI]
public static class RandUtils {
    private const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    public static readonly Random R = new();
    
    /// <summary>
    /// Create a random string. Uses the chars a-zA-Z0-9 if no set is provided.
    /// </summary>
    public static string RandString(this Random r, int len, string? chars = null) {
        chars ??= CHARS;
        var stringChars = new char[len];
        for (int i = 0; i < stringChars.Length; i++) {
            stringChars[i] = chars[r.Next(chars.Length)];
        }
        return new string(stringChars);
    }
}
}