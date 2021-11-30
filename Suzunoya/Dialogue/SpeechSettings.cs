using System;
using System.Globalization;
using BagoumLib;
using BagoumLib.Events;

namespace Suzunoya.Dialogue {
public record SpeechSettings(float opsPerSecond, Func<string, int, float> opsPerChar, float opsPerRollEvent, Func<string, int, bool> rollEventAllowed, Action? rollEvent) {

    //For consumers without with expressions :(
    public SpeechSettings(SpeechSettings copy) {
        opsPerSecond = copy.opsPerSecond;
        opsPerChar = copy.opsPerChar;
        opsPerRollEvent = copy.opsPerRollEvent;
        rollEventAllowed = copy.rollEventAllowed;
        rollEvent = copy.rollEvent;
    }

    private static bool IsEllipses(string s, int index) {
        return s[index] == '.' && 
               (s.TryIndex(index + 1) == '.' || s.TryIndex(index - 1) == '.');
    }

    private static bool IsSequentialQE(string s, int index) =>
        s[index] is '!' or '?' && s.TryIndex(index - 1) is '!' or '?';

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

    public static readonly SpeechSettings Default =
        new SpeechSettings(60, DefaultOpsPerChar, 8, DefaultRollEventAllowed, null);

}
}