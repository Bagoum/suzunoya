﻿using System;
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
            '\n' => 2,
            { } when char.IsWhiteSpace(ch) => 1.5f,
            ',' => 2,
            ';' => 2.5f,
            ':' => 2.5f,
            '!' => 3.5f,
            '?' => 3.5f,
            '.' => IsEllipses(s, index) ? 2 : 3.5f,
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
        new SpeechSettings(30, DefaultOpsPerChar, 8, DefaultRollEventAllowed, null);

}
}