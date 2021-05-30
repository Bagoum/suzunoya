using System;
using BagoumLib;

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

    public static float DefaultOpsPerChar(string s, int index) {
        var ch = s[index];
        return ch switch {
            '\n' => 4,
            { } when char.IsWhiteSpace(ch) => 3,
            ',' => 4,
            ';' => 5,
            ':' => 5,
            '!' => 7,
            '?' => 7,
            '.' => IsEllipses(s, index) ? 4 : 7,
            _ => 1
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
        new SpeechSettings(10, DefaultOpsPerChar, 6, DefaultRollEventAllowed, null);

}
}