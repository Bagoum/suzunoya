using System;
using System.Reactive;
using BagoumLib.Functional;

namespace Mizuhashi {
public static partial class Combinators {
    public static readonly ParserError expectedEof = new ParserError.Expected("end of file");
    public static readonly ParserError unexpectedEof = new ParserError.Unexpected("end of file");
    public static readonly ParserError atleastOneWhitespace = new ParserError.Expected("at least one whitespace");
    public static readonly ParserError expectedNL = new ParserError.Expected("newline");
    public static readonly ParserError partialNLR = 
        new ParserError.Labelled("newline", new("Expected \\r to be followed by \\n."));
    
    /// <summary>
    /// Fails if the stream is not at end-of-file.
    /// </summary>
    public static readonly Parser<Unit> EOF =
        input => input.Empty ?
            new ParseResult<Unit>(new(Unit.Default), null, input.Index, input.Index) :
            new(expectedEof, input.Index);

    /// <summary>
    /// Fail if the stream is at end-of-file. If it is not, then run the nested parser.
    /// </summary>
    private static Parser<R> NotEOF<R>(this Parser<R> p) =>
        input => input.Empty ?
            new ParseResult<R>(unexpectedEof, input.Index) :
            p(input);

    /// <summary>
    /// Parse a specific character.
    /// </summary>
    /// <param name="c">Character</param>
    /// <param name="expected">Description of character (generally only necessary for characters that are
    /// not well displayed, such as space or tab)</param>
    /// <returns></returns>
    public static Parser<char> Char(char c, ParserError? expected = null) {
        expected ??= new ParserError.ExpectedChar(c);
        return input => {
            if (input.MaybeNext == c)
                //Don't need to store errors if the consumption is nonempty
                return new(new(c), null, input.Index, input.Step(1));
            else
                return new(expected, input.Index);
        };
    }

    /// <summary>
    /// Return the next character in the stream, or null if it empty, without advancing the stream.
    /// </summary>
    public static Parser<char?> SoftScan = input =>
        new(input.Empty ? null : input.Next, null, input.Index, input.Index);
    
    /// <summary>
    /// Parse any character (but not EOF).
    /// </summary>
    public static Parser<char> AnyChar() {
        var err = new ParserError.Expected("any character");
        return input => input.Empty ?
            new(err, input.Index) :
            new(new(input.Next), null, input.Index, input.Step(1));
    }

    /// <summary>
    /// Parse a character that satisfies the predicate.
    /// </summary>
    /// <param name="pred">Predicate</param>
    /// <param name="expected">Description of what the predicate does</param>
    public static Parser<char> Satisfy(Func<char, bool> pred, ParserError? expected = null) {
        return input => {
            if (input.Empty || !pred(input.Next))
                //In general, it is more informative to say "Expected X, Y, or Z" than to say "Didn't expect EOF".
                return new(expected ?? (input.Empty ? 
                    unexpectedEof : 
                    new ParserError.UnexpectedChar(input.Next)), input.Index, input.Index);
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
    public static Parser<string> ManySatisfy(Func<char, bool> pred, bool atleastOne = false, string? expected = null) {
        var atleastErr = new ParserError.Expected($"at least one {expected}");
        return input => {
            var len = 0;
            for (; len < input.Remaining; ++len) {
                if (!pred(input.CharAt(len)))
                    break;
            }
            if (len == 0 && atleastOne)
                return new(atleastErr, input.Index);
            return new(new(input.Substring(len)),
                len == 0 ? input.MakeError(expected) : null, input.Index, input.Step(len));
        };
    }

    /// <summary>
    /// ManySatisfy with atleastOne set to true.
    /// </summary>
    public static Parser<string> Many1Satisfy(Func<char, bool> pred, string? expected = null) =>
        ManySatisfy(pred, true, expected);

    
    /// <summary>
    /// Sequentially parse as many characters as possible that satisfy the predicate,
    ///  but returns nothing.
    /// </summary>
    /// <param name="pred">Predicate</param>
    /// <param name="atleastOne">If true, will require at least one match</param>
    /// <param name="expected">Description of what the predicate does</param>
    public static Parser<Unit> SkipManySatisfy(Func<char, bool> pred, bool atleastOne = false, string? expected = null) {
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
    public static Parser<Unit> SkipMany1Satisfy(Func<char, bool> pred, string? expected = null) =>
        SkipManySatisfy(pred, true, expected);

    private static Parser<Unit> _Whitespace(bool atleastOne=false) => input => {
        var skip = 0;
        for (; skip < input.Remaining; ++skip) {
            if (!char.IsWhiteSpace(input.CharAt(skip)))
                break;
        }
        if (skip == 0 && atleastOne)
            return new(atleastOneWhitespace, input.Index);
        return new(new(Unit.Default), null, input.Index, input.Step(skip));
    };
    
    /// <summary>
    /// Parses zero or more whitespaces (including newlines).
    /// <br/>FParsec spaces
    /// </summary>
    public static readonly Parser<Unit> Whitespace = _Whitespace(false);

    /// <summary>
    /// Parses one or more whitespaces (including newlines).
    /// <br/>FParsec spaces1
    /// </summary>
    public static readonly Parser<Unit> Whitespace1 = _Whitespace(true);

    /// <summary>
    /// Parses \r\n or \n, and returns \n.
    /// </summary>
    public static readonly Parser<char> Newline =
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
    public static Parser<char> Tab() => Char('\t', "Tab");

    public static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    /// <summary>
    /// Parse a-z.
    /// </summary>
    public static readonly Parser<char> AsciiLower = 
        Satisfy(c => c >= 'a' && c <= 'z', "lowercase ASCII");
    /// <summary>
    /// Parse A-Z.
    /// </summary>
    public static readonly Parser<char> AsciiUpper = 
        Satisfy(c => c >= 'A' && c <= 'Z', "uppercase ASCII");
    /// <summary>
    /// Parse a-zA-Z.
    /// </summary>
    public static readonly Parser<char> AsciiLetter = 
        Satisfy(IsAsciiLetter, "ASCII");
    /// <summary>
    /// Parse any lowercase character.
    /// </summary>
    public static readonly Parser<char> Lower = 
        Satisfy(char.IsLower, "lowercase character");
    /// <summary>
    /// Parse any uppercase character.
    /// </summary>
    public static readonly Parser<char> Upper = 
        Satisfy(char.IsUpper, "uppercase character");
    /// <summary>
    /// Parse any letter.
    /// </summary>
    public static readonly Parser<char> Letter = 
        Satisfy(char.IsLetter, "letter");
    /// <summary>
    /// Parse any letter or digit.
    /// </summary>
    public static readonly Parser<char> LetterOrDigit = 
        Satisfy(char.IsLetterOrDigit, "letter or digit");
    /// <summary>
    /// Parse any digit.
    /// </summary>
    public static readonly Parser<char> Digit = 
        Satisfy(char.IsDigit, "digit");
    /// <summary>
    /// Parse a-fA-F0-9.
    /// </summary>
    public static readonly Parser<char> Hex = 
        Satisfy(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c>= 'A' && c <= 'F'), "hex");
    /// <summary>
    /// Parse 0-7.
    /// </summary>
    public static readonly Parser<char> Octal = 
        Satisfy(c => c >= '0' && c <= '7', "octal");


    /// <summary>
    /// Parses a string.
    /// </summary>
    public static Parser<string> String(string s) {
        if (s.Length == 0) return PReturn(s);
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
    /// Matches a string (case-insensitive). Returns the parsed string.
    /// </summary>
    public static Parser<string> StringCI(string s) {
        if (s.Length == 0) return PReturn<string>(s);
        var err = new ParserError.Expected($"\"{s}\"");
        return inp => {
            s = s.ToLower();
            if (inp.Remaining < s.Length)
                return new(err, inp.Index);
            for (int ii = 0; ii < s.Length; ++ii) {
                if (char.ToLower(inp.CharAt(ii)) != s[ii])
                    return new(err, inp.Index);
            }
            return new(new(inp.Substring(s.Length)), null, inp.Index, inp.Step(s.Length));
        };
    }

    /// <summary>
    /// Parse a sequence of digits and converts them into an integer.
    /// </summary>
    public static readonly Parser<int> ParseInt = Many1Satisfy(char.IsDigit, "digit").FMap(int.Parse);


}
}