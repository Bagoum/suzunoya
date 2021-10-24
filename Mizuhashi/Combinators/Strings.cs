using System;
using System.Reactive;
using BagoumLib.Functional;

namespace Mizuhashi {
public static partial class Combinators {
    private static readonly ParserError expectedEof = new ParserError.Expected("end of file");
    private static readonly ParserError unexpectedEof = new ParserError.Unexpected("end of file");
    private static readonly ParserError atleastOneWhitespace = new ParserError.Expected("at least one whitespace");
    private static readonly ParserError expectedNL = new ParserError.Expected("newline");
    private static readonly ParserError partialNLR = 
        new ParserError.Labelled("newline", new("Expected \\r to be followed by \\n."));
    
    public static Parser<Unit, S> EOF<S>() =>
        input => input.Empty ?
            new ParseResult<Unit, S>(new(Unit.Default), null, input, input) :
            new(expectedEof, input);

    private static Parser<R, S> NotEOF<R, S>(this Parser<R, S> p) =>
        input => input.Empty ?
            new ParseResult<R, S>(unexpectedEof, input) :
            p(input);

    public static Parser<char, S> Char<S>(char c, ParserError? expected = null) {
        expected ??= new ParserError.ExpectedChar(c);
        return NotEOF<char, S>(input => {
            if (input.Next == c)
                //Don't need to store errors if the consumption is nonempty
                return new(new(c), null, input, input.Step(1));
            else
                return new(Maybe<char>.None, input.MakeError(expected), input, input);
        });
    }
    
    public static Parser<char, S> AnyChar<S>() => 
        NotEOF<char, S>(input => new(new(input.Next), null, input, input.Step(1)));

    public static Parser<char, S> Satisfy<S>(Func<char, bool> pred, ParserError? expected = null) {
        return NotEOF<char, S>(input => {
            if (pred(input.Next))
                return new(new(input.Next), null, input, input.Step(1));
            else
                return new(Maybe<char>.None, 
                    input.MakeError(expected ?? new ParserError.UnexpectedChar(input.Next)), input, input);
        });
    }
    
    public static Parser<string, S> ManySatisfy<S>(Func<char, bool> pred, bool atleastOne = false, string? expected = null) {
        return input => {
            var len = 0;
            for (; len < input.Remaining; ++len) {
                if (!pred(input.CharAt(len)))
                    break;
            }
            if (len == 0 && atleastOne)
                return new(new ParserError.Expected($"at least one {expected}"), input);
            return new(new(input.Substring(len)), input.MakeError(expected), input, input.Step(len));
        };
    }

    public static Parser<string, S> Many1Satisfy<S>(Func<char, bool> pred, string? expected = null) =>
        ManySatisfy<S>(pred, true, expected);
    
    

    /// <summary>
    /// Parses zero or more whitespaces.
    /// <br/>FParsec spaces
    /// </summary>
    public static Parser<Unit, S> Whitespace<S>(bool atleastOne=false) => input => {
        var skip = 0;
        for (; skip < input.Remaining; ++skip) {
            if (!char.IsWhiteSpace(input.CharAt(skip)))
                break;
        }
        if (skip == 0 && !atleastOne)
            return new(atleastOneWhitespace, input);
        return new(new(Unit.Default), null, input, input.Step(skip));
    };

    /// <summary>
    /// Parses one or more whitespaces.
    /// <br/>FParsec spaces1
    /// </summary>
    public static Parser<Unit, S> Whitespace1<S>() => Whitespace<S>(true);

    /// <summary>
    /// Accepts \r\n or \n, and returns \n.
    /// </summary>
    public static Parser<char, S> Newline<S>() {
        return NotEOF<char, S>(input => input.Next switch {
            '\n' => new(new('\n'), null, input, input.Step(1)),
            '\r' => input.TryCharAt(1, out var n) ?
                n == '\n' ? 
                    new(new('\n'), null, input, input.Step(2)) :
                    new(partialNLR, input) :
                new(partialNLR, input),
            _ => new(expectedNL, input)
        });
    }

    public static Parser<char, S> Tab<S>() => Char<S>('\t', "Tab");

    public static Parser<char, S> AsciiLower<S>() => 
        Satisfy<S>(c => c >= 'a' && c <= 'z', "lowercase ASCII");
    public static Parser<char, S> AsciiUpper<S>() => 
        Satisfy<S>(c => c >= 'A' && c <= 'Z', "uppercase ASCII");
    public static Parser<char, S> Ascii<S>() => 
        Satisfy<S>(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'), "ASCII");
    public static Parser<char, S> Lower<S>() => 
        Satisfy<S>(char.IsLower, "lowercase character");
    public static Parser<char, S> Upper<S>() => 
        Satisfy<S>(char.IsUpper, "uppercase character");
    public static Parser<char, S> Letter<S>() => 
        Satisfy<S>(char.IsLetter, "letter");
    public static Parser<char, S> LetterOrDigit<S>() => 
        Satisfy<S>(char.IsLetterOrDigit, "letter or digit");
    public static Parser<char, S> Digit<S>() => 
        Satisfy<S>(char.IsDigit, "digit");
    public static Parser<char, S> Hex<S>() => 
        Satisfy<S>(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c>= 'A' && c <= 'F'), "hex");
    public static Parser<char, S> Octal<S>() => 
        Satisfy<S>(c => c >= '0' && c <= '7', "octal");


    /// <summary>
    /// Matches a string.
    /// </summary>
    public static Parser<string, S> String<S>(string s) {
        if (s.Length == 0) return PReturn<string, S>(s);
        var err = new ParserError.Expected($"\"{s}\"");
        return inp => {
            if (inp.Remaining < s.Length)
                return new(err, inp);
            for (int ii = 0; ii < s.Length; ++ii) {
                if (inp.CharAt(ii) != s[ii])
                    return new(err, inp);
            }
            return new(new(s), null, inp, inp.Step(s.Length));
        };
    }
    
    /// <summary>
    /// Matches a string (case-insensitive). Returns the parsed string.
    /// </summary>
    public static Parser<string, S> StringCI<S>(string s) {
        if (s.Length == 0) return PReturn<string, S>(s);
        var err = new ParserError.Expected($"\"{s}\"");
        return inp => {
            s = s.ToLower();
            if (inp.Remaining < s.Length)
                return new(err, inp);
            for (int ii = 0; ii < s.Length; ++ii) {
                if (char.ToLower(inp.CharAt(ii)) != s[ii])
                    return new(err, inp);
            }
            return new(new(inp.Substring(s.Length)), null, inp, inp.Step(s.Length));
        };
    }

    public static Parser<int, S> ParseInt<S>() => Many1Satisfy<S>(char.IsDigit, "digit").FMap(int.Parse);


}
}