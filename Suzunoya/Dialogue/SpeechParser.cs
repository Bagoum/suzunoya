using System;
using System.Collections.Generic;
using BagoumLib.Functional;
using Mizuhashi;
using static Mizuhashi.Combinators;

namespace Suzunoya.Dialogue {
internal abstract record TextUnit {
    public record String(string fragment) : TextUnit;
        
    public record OpenTag(string name, string? content = null) : TextUnit;

    public record CloseTag(string name) : TextUnit;
}

public static partial class SpeechParser {
    private static bool TagChar(char c) =>
        c == TAG_OPEN || c == TAG_CLOSE;

    private static bool NotTagChar(char c) => !TagChar(c);
    private static bool NotTagOrEscapeChar(char c) => !TagChar(c) && c != ESCAPER;

    private static readonly Parser<char, TextUnit> escapedFragment =
        Sequential(Char(ESCAPER), AnyChar(), (_, c) => 
            (TextUnit)new TextUnit.String(c.ToString()));

    private static readonly Parser<char, TextUnit> normalFragment =
        Many1Satisfy(NotTagOrEscapeChar).FMap(s => (TextUnit)new TextUnit.String(s));

    private static readonly Parser<char, TextUnit> tagFragment =
        Between(TAG_OPEN, Many1Satisfy(NotTagChar), TAG_CLOSE).FMap(s => {
            int contInd;
            if (s[0] == TAG_CLOSE_PREFIX)
                return (TextUnit)new TextUnit.CloseTag(s.Substring(1));
            else if ((contInd = s.IndexOf(TAG_CONTENT)) > 0)
                return new TextUnit.OpenTag(s.Substring(0, contInd), s.Substring(contInd + 1));
            else
                return new TextUnit.OpenTag(s, null);
        });
        

    private static readonly Parser<char, List<TextUnit>> speechParser = Choice(
        tagFragment,
        normalFragment,
        escapedFragment
    ).Many1();

    /// <summary>
    /// Parse rich text tags out of a raw string.
    /// </summary>
    /// <param name="raw">Raw string</param>
    /// <returns>Parsed content</returns>
    /// <exception cref="Exception">Thrown if parsing fails</exception>
    internal static List<TextUnit> Parse(string raw) => 
        speechParser.ResultOrErrorString(new InputStream<char>("Dialogue text", raw.ToCharArray(), null!))
            .Map(l => l, r => throw new Exception(r));
    }
}
