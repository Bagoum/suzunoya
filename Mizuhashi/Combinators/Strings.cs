﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace Mizuhashi {
public static partial class Combinators {
    /// <summary>
    /// Error when an end-of-file was expected but not found.
    /// </summary>
    public static readonly ParserError expectedEof = new ParserError.Expected("end of file");
    /// <summary>
    /// Error when an end-of-file was found but not expect.
    /// </summary>
    public static readonly ParserError unexpectedEof = new ParserError.Unexpected("end of file");
    /// <summary>
    /// Error when whitespace was expected but not found.
    /// </summary>
    public static readonly ParserError atleastOneWhitespace = new ParserError.Expected("at least one whitespace");
    /// <summary>
    /// Error when a newline was expected but not found.
    /// </summary>
    public static readonly ParserError expectedNL = new ParserError.Expected("newline");
    /// <summary>
    /// Error when \r was found but \n did not follow it.
    /// </summary>
    public static readonly ParserError partialNLR = 
        new ParserError.Labelled("newline", new("Expected \\r to be followed by \\n."));
    
    /// <summary>
    /// Fails if the stream is not at end-of-file.
    /// </summary>
    public static Parser<T, Unit> EOF<T>() =>
        input => input.Empty ?
            new ParseResult<Unit>(new(Unit.Default), null, input.Index, input.Index) :
            new(expectedEof, input.Index);

    /// <summary>
    /// Parse a specific character.
    /// </summary>
    /// <param name="c">Character</param>
    /// <param name="expected">Description of character (generally only necessary for characters that are
    /// not well displayed, such as space or tab)</param>
    /// <returns></returns>
    public static Parser<char, char> Char(char c, string? expected = null) {
        var err = expected == null ? 
            (ParserError)new ParserError.ExpectedChar(c) : 
            new ParserError.Expected(expected);
        return input => {
            if (input.MaybeNext == c)
                //Don't need to store errors if the consumption is nonempty
                return new(new(c), null, input.Index, input.Step(1));
            else
                return new(err, input.Index);
        };
    }
    
    /// <summary>
    /// Parse any character except the one provided character.
    /// <param name="c">Character</param>
    /// <param name="expected">Description of character (generally only necessary for characters that are
    /// not well displayed, such as space or tab)</param>
    /// </summary>
    public static Parser<char, char> NotChar(char c, string? expected = null) {
        var err = expected == null ? 
            (ParserError)new ParserError.ExpectedChar(c) : 
            new ParserError.Expected(expected);
        return input => {
            if (input.Empty || input.Next == c)
                return new(err, input.Index);
            return new(input.Next, null, input.Index, input.Step(1));
        };
    }

    /// <summary>
    /// Return the next character in the stream, or null if it empty, without advancing the stream.
    /// <br/>This parser does not fail.
    /// </summary>
    public static Parser<char, char?> SoftScan = input =>
        new(input.Empty ? null : input.Next, null, input.Index, input.Index);
    
    /// <summary>
    /// Parse any character (but not EOF).
    /// </summary>
    public static Parser<char, char> AnyChar() {
        var err = new ParserError.Expected("any character");
        return input => input.Empty ?
            new(err, input.Index) :
            new(new(input.Next), null, input.Index, input.Step(1));
    }

    /// <summary>
    /// Parse any of the provided characters.
    /// </summary>
    public static Parser<char, char> AnyOf(IReadOnlyList<char> chars) {
        var expected = new ParserError.Expected($"any of {string.Join(", ", chars)}");
        var set = chars.ToHashSet();
        return input => {
            if (input.Empty || !set.Contains(input.Next))
                return new(expected, input.Index);
            return new(input.Next, null, input.Index, input.Step(1));
        };
    }

    /// <summary>
    /// Parse any character except the provided characters.
    /// </summary>
    public static Parser<char, char> NoneOf(IReadOnlyList<char> chars) {
        var expected = new ParserError.Expected($"any except {string.Join(", ", chars)}");
        var set = chars.ToHashSet();
        return input => {
            if (input.Empty || set.Contains(input.Next))
                return new(expected, input.Index);
            return new(input.Next, null, input.Index, input.Step(1));
        };
    }


    /// <summary>
    /// Parse a token that satisfies the predicate.
    /// </summary>
    /// <param name="pred">Predicate</param>
    /// <param name="expected">Description of what the predicate does</param>
    public static Parser<T, T> Satisfy<T>(Func<T, bool> pred, string? expected = null) {
        var err = expected == null ? null : new ParserError.Expected(expected);
        return input => {
            if (input.Empty || !pred(input.Next))
                //In general, it is more informative to say "Expected X, Y, or Z" than to say "Didn't expect EOF".
                return new(err ?? (input.Empty ? 
                    unexpectedEof : 
                    input.TokenWitness.Unexpected(input.Index)), input.Index);
            else
                return new(new(input.Next), null, input.Index, input.Step(1));
        };
    }
    

    /// <summary>
    /// Parse a token that doesn't satisfy the predicate.
    /// </summary>
    /// <param name="pred">Predicate</param>
    /// <param name="unexpected">Description of what the predicate does</param>
    public static Parser<T, T> DontSatisfy<T>(Func<T, bool> pred, string? unexpected = null) {
        var err = unexpected == null ? null : new ParserError.Unexpected(unexpected);
        return input => {
            if (input.Empty || pred(input.Next))
                //In general, it is more informative to say "Unexpected X, Y, or Z" than to say "Didn't expect EOF".
                return new(err ?? (input.Empty ? 
                    unexpectedEof : 
                    input.TokenWitness.Unexpected(input.Index)), input.Index);
            else
                return new(new(input.Next), null, input.Index, input.Step(1));
        };
    }

    /// <inheritdoc cref="Satisfy{T}(System.Func{T,bool},string?)"/>
    public static Parser<char, char> Satisfy(Func<char, bool> pred, string? expected = null) =>
        Satisfy<char>(pred, expected);
    
    /// <summary>
    /// Parse the next character when the input stream satisfies the predicate.
    /// </summary>
    /// <param name="pred">Predicate</param>
    /// <param name="expected">Description of what the predicate does</param>
    public static Parser<T, T> Satisfy<T>(Func<InputStream<T>, bool> pred, string? expected = null) {
        var err = expected == null ? null : new ParserError.Expected(expected);
        return input => {
            if (input.Empty || !pred(input))
                //In general, it is more informative to say "Expected X, Y, or Z" than to say "Didn't expect EOF".
                return new(err ?? (input.Empty ? 
                    unexpectedEof : 
                    input.TokenWitness.Unexpected(input.Index)), input.Index);
            else
                return new(new(input.Next), null, input.Index, input.Step(1));
        };
    }
    
    /// <summary>
    /// Sequentially parse as many characters as possible that satisfy the predicate,
    /// and returns a string composed of them.
    /// </summary>
    /// <param name="pred">Predicate</param>
    /// <param name="atleastOne">If true, will require at least one match</param>
    /// <param name="expected">Description of what the predicate does</param>
    public static Parser<char, string> ManySatisfy(Func<char, bool> pred, bool atleastOne = false, string? expected = null) {
        var atleastErr = new ParserError.Expected($"at least one {expected}");
        return input => {
            var len = 0;
            for (; len < input.Remaining; ++len) {
                if (!pred(input.CharAt(len)))
                    break;
            }
            if (len == 0 && atleastOne)
                return new(atleastErr, input.Index);
            return new(new(Substring(input, len)),
                len == 0 ? input.MakeError(expected) : null, input.Index, input.Step(len));
        };
    }
    
    /// <summary>
    /// Sequentially parse as many characters as possible while the input stream satisfies the predicate,
    /// and returns a string composed of them.
    /// </summary>
    /// <param name="pred">Predicate</param>
    /// <param name="atleastOne">If true, will require at least one match</param>
    /// <param name="expected">Description of what the predicate does</param>
    public static Parser<char, string> ManySatisfyI(Func<InputStream<char>, bool> pred, bool atleastOne = false, string? expected = null) {
        var atleastErr = new ParserError.Expected($"at least one {expected}");
        return input => {
            var len = 0;
            var start = input.Index;
            for (; !input.Empty; ++len) {
                if (!pred(input))
                    break;
                input.Step(1);
            }
            if (len == 0 && atleastOne)
                return new(atleastErr, start);
            return new(new(Substring(input, -len, len)),
                len == 0 ? input.MakeError(expected) : null, start, input.Index);
        };
    }

    /// <summary>
    /// ManySatisfy with atleastOne set to true.
    /// </summary>
    public static Parser<char, string> Many1Satisfy(Func<char, bool> pred, string? expected = null) =>
        ManySatisfy(pred, true, expected);

    
    /// <summary>
    /// Sequentially parse as many characters as possible that satisfy the predicate,
    ///  but returns nothing.
    /// </summary>
    /// <param name="pred">Predicate</param>
    /// <param name="atleastOne">If true, will require at least one match</param>
    /// <param name="expected">Description of what the predicate does</param>
    public static Parser<char, Unit> SkipManySatisfy(Func<char, bool> pred, bool atleastOne = false, string? expected = null) {
        var atleastErr = new ParserError.Expected($"at least one {expected}");
        return input => {
            var len = 0;
            for (; len < input.Remaining; ++len) {
                if (!pred(input.CharAt(len)))
                    break;
            }
            if (len == 0 && atleastOne)
                return new(atleastErr, input.Index);
            return new(Unit.Default,
                len == 0 ? input.MakeError(expected) : null, input.Index, input.Step(len));
        };
    }

    /// <summary>
    /// SkipManySatisfy with atleastOne set to true.
    /// </summary>
    public static Parser<char, Unit> SkipMany1Satisfy(Func<char, bool> pred, string? expected = null) =>
        SkipManySatisfy(pred, true, expected);

    private static Parser<char, Unit> _Whitespace(bool allowNewline, bool atleastOne) => input => {
        var skip = 0;
        for (; skip < input.Remaining; ++skip) {
            if (!char.IsWhiteSpace(input.CharAt(skip)) || (!allowNewline && input.CharAt(skip) == '\n'))
                break;
        }
        if (skip == 0 && atleastOne)
            return new(atleastOneWhitespace, input.Index);
        return new(new(Unit.Default), null, input.Index, input.Step(skip));
    };
    
    /// <summary>
    /// Parses zero or more whitespaces (including newlines).
    /// This parser cannot fail.
    /// <br/>FParsec spaces
    /// </summary>
    public static readonly Parser<char, Unit> Whitespace = _Whitespace(true, false);
    /// <summary>
    /// Parses zero or more whitespaces (not including newlines).
    /// This parser cannot fail.
    /// </summary>
    public static readonly Parser<char, Unit> WhitespaceIL = _Whitespace(false, false);

    /// <summary>
    /// Parses one or more whitespaces (including newlines).
    /// <br/>FParsec spaces1
    /// </summary>
    public static readonly Parser<char, Unit> Whitespace1 = _Whitespace(true, true);
    /// <summary>
    /// Parses one or more whitespaces (not including newlines).
    /// </summary>
    public static readonly Parser<char, Unit> WhitespaceIL1 = _Whitespace(false, true);

    /// <summary>
    /// Parses \r\n or \n, and returns \n.
    /// </summary>
    public static readonly Parser<char, char> Newline =
        input => {
            return input.MaybeNext switch {
                '\n' => new(new('\n'), null, input.Index, input.Step(1)),
                '\r' => input.TryCharAt(1, out var n) ?
                    n == '\n' ?
                        new(new('\n'), null, input.Index, input.Step(2)) :
                        new(partialNLR, input.Index) :
                    new(partialNLR, input.Index),
                _ => new(expectedNL, input.Index)
            };
        };

    /// <summary>
    /// Parses \t.
    /// </summary>
    public static Parser<char, char> Tab() => Char('\t', "Tab");

    /// <summary>
    /// Parses the provided regex. Automatically prepends a `\G` to the regex so it only matches from the start.
    /// </summary>
    public static Parser<char, Match> Regex([RegexPattern] string pattern, RegexOptions flags = RegexOptions.Compiled) {
        var r = new Regex(@"\G" + pattern, flags);
        var err = new ParserError.Expected($"regex match for pattern /{pattern}/");
        return input => {
            var m = r.Match(input.SourceAsString!, input.Index);
            if (m.Success) {
                return new(new(m), null, input.Index, input.Step(m.Length));
            } else {
                return new(err, input.Index);
            }
        };
    }

    /// <summary>
    /// True iff a character is [a-zA-Z].
    /// </summary>
    public static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    /// <summary>
    /// Parse a-z.
    /// </summary>
    public static readonly Parser<char, char> AsciiLower = 
        Satisfy(c => c >= 'a' && c <= 'z', "lowercase ASCII");
    /// <summary>
    /// Parse A-Z.
    /// </summary>
    public static readonly Parser<char, char> AsciiUpper = 
        Satisfy(c => c >= 'A' && c <= 'Z', "uppercase ASCII");
    /// <summary>
    /// Parse a-zA-Z.
    /// </summary>
    public static readonly Parser<char, char> AsciiLetter = 
        Satisfy(IsAsciiLetter, "ASCII");
    /// <summary>
    /// Parse any lowercase character.
    /// </summary>
    public static readonly Parser<char, char> Lower = 
        Satisfy(char.IsLower, "lowercase character");
    /// <summary>
    /// Parse any uppercase character.
    /// </summary>
    public static readonly Parser<char, char> Upper = 
        Satisfy(char.IsUpper, "uppercase character");
    /// <summary>
    /// Parse any letter.
    /// </summary>
    public static readonly Parser<char, char> Letter = 
        Satisfy(char.IsLetter, "letter");
    /// <summary>
    /// Parse any letter or digit.
    /// </summary>
    public static readonly Parser<char, char> LetterOrDigit = 
        Satisfy(char.IsLetterOrDigit, "letter or digit");
    /// <summary>
    /// Parse any digit.
    /// </summary>
    public static readonly Parser<char, char> Digit = 
        Satisfy(char.IsDigit, "digit");
    /// <summary>
    /// Parse a-fA-F0-9.
    /// </summary>
    public static readonly Parser<char, char> Hex = 
        Satisfy(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c>= 'A' && c <= 'F'), "hex");
    /// <summary>
    /// Parse 0-7.
    /// </summary>
    public static readonly Parser<char, char> Octal = 
        Satisfy(c => c >= '0' && c <= '7', "octal");


    /// <summary>
    /// Parses a string.
    /// </summary>
    public static Parser<char, string> String(string s) {
        if (s.Length == 0) return PReturn<char, string>(s);
        var err = new ParserError.Expected($"\"{s}\"");
        return inp => {
            if (inp.Remaining < s.Length)
                return new(err, inp.Index);
            for (int ii = 0; ii < s.Length; ++ii) {
                if (inp.CharAt(ii) != s[ii])
                    return new(err, inp.Index);
            }
            return new(new(s), null, inp.Index, inp.Step(s.Length));
        };
    }
    
    
    /// <summary>
    /// Parses a string, but returns nothing.
    /// </summary>
    public static Parser<char, Unit> StringIg(string s) {
        if (s.Length == 0) return Ignore<char>();
        var err = new ParserError.Expected($"\"{s}\"");
        return inp => {
            if (inp.Remaining < s.Length)
                return new(err, inp.Index);
            for (int ii = 0; ii < s.Length; ++ii) {
                if (inp.CharAt(ii) != s[ii])
                    return new(err, inp.Index);
            }
            return new(Maybe<Unit>.Of(default), null, inp.Index, inp.Step(s.Length));
        };
    }
    
    /// <summary>
    /// Matches a string (case-insensitive). Returns the parsed string.
    /// </summary>
    public static Parser<char, string> StringCI(string s) {
        if (s.Length == 0) return PReturn<char, string>(s);
        var err = new ParserError.Expected($"\"{s}\"");
        return inp => {
            s = s.ToLower();
            if (inp.Remaining < s.Length)
                return new(err, inp.Index);
            for (int ii = 0; ii < s.Length; ++ii) {
                if (char.ToLower(inp.CharAt(ii)) != s[ii])
                    return new(err, inp.Index);
            }
            return new(new(Substring(inp, s.Length)), null, inp.Index, inp.Step(s.Length));
        };
    }

    /// <summary>
    /// Parse a sequence of digits and converts them into an integer.
    /// </summary>
    public static readonly Parser<char, int> ParseInt = Many1Satisfy(char.IsDigit, "digit").FMap(int.Parse);
    
    /// <summary>
    /// Get a substring from a string stream.
    /// </summary>
    public static string Substring(this InputStream<char> stream, int len) =>
        new(stream.Source.AsSpan(stream.Index, len));
    
    /// <summary>
    /// Get a substring from a string stream.
    /// </summary>
    public static string Substring(this InputStream<char> stream, int offset, int len) =>
        new(stream.Source.AsSpan(stream.Index + offset, len));
}
}