using System;
using System.Globalization;
using BagoumLib;
using BagoumLib.Events;

namespace Suzunoya.Dialogue {
/// <summary>
/// Settings configured how speech text is played out over time.
/// </summary>
/// <param name="opsPerSecond">The number of speech "operations" per second that are executed.</param>
/// <param name="opsPerChar">Mapping from a character to a number of "operations" that character requires.</param>
/// <param name="opsPerRollEvent">The minimum number of operations between invocations of `rollEvent`.</param>
/// <param name="rollEventAllowed">Whether or not a roll event can be triggered for a given character.</param>
/// <param name="rollEvent">An event that is periodically triggered when enough operations are accumulated.</param>
public record SpeechSettings(float opsPerSecond, Func<string, int, float> opsPerChar, float opsPerRollEvent, Func<string, int, bool> rollEventAllowed, Action? rollEvent) {

    private static bool IsEllipses(string s, int index) {
        return s[index] == '.' && 
               (s.TryIndex(index + 1) == '.' || s.TryIndex(index - 1) == '.');
    }

    private static bool IsSequentialQE(string s, int index) =>
        s[index] is '!' or '?' && s.TryIndex(index - 1) is '!' or '?';

    /// <summary>
    /// Default behavior mapping characters to operation counts.
    /// <br/>ASCII letter = 1, non-ASCII letter = 3
    /// <br/>whitespace = 2, newline = 8
    /// <br/>comma = 5, semicolon/colon = 8
    /// <br/>period = 10 (7 if sequential), exclamation/question = 10 (5 if sequential)
    /// </summary>
    public static float DefaultOpsPerChar(string s, int index) {
        var ch = s[index];
        return ch switch {
            '\n' => 8,
            { } when char.IsWhiteSpace(ch) => 2f,
            ',' => 5,
            ';' => 8f,
            ':' => 8f,
            '!' => IsSequentialQE(s, index) ? 5f : 10f,
            '?' => IsSequentialQE(s, index) ? 5f : 10f,
            '.' => IsEllipses(s, index) ? 7 : 10f,
            { } when char.GetUnicodeCategory(ch) == UnicodeCategory.OtherLetter => 3f,
            _ => 1,
        };
    }

    /// <summary>
    /// Default provider for <see cref="rollEventAllowed"/>, disallowing roll events on punctuation and whitespace.
    /// </summary>
    public static bool DefaultRollEventAllowed(string s, int index) {
        var ch = s[index];
        return ch switch {
            { } when char.IsWhiteSpace(ch) => false,
            ',' => false,
            ';' => false,
            ':' => false,
            '!' => false,
            '?' => false,
            '.' => false,
            _ => true
        };
    }

    /// <summary>
    /// Default settings.
    /// </summary>
    public static readonly SpeechSettings Default =
        new(60, DefaultOpsPerChar, 8, DefaultRollEventAllowed, null);

}
}