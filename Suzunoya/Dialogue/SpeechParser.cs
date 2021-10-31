using System;
using System.Collections.Generic;
using BagoumLib.Functional;
using Mizuhashi;
using static Mizuhashi.Combinators;

namespace Suzunoya.Dialogue {
public abstract record TextUnit {
    public record String(string fragment) : TextUnit;
        
    public record OpenTag(string name, string? content = null) : TextUnit;

    public record CloseTag(string name) : TextUnit;
}

public static partial class SpeechParser {
    private static bool TagChar(char c) =>
        c == TAG_OPEN || c == TAG_CLOSE;

    private static bool NotTagChar(char c) => !TagChar(c);
    private static bool NotTagOrEscapeChar(char c) => !TagChar(c) && c != ESCAPER;

    private static readonly Parser<TextUnit> escapedFragment =
        Sequential(Char(ESCAPER), Satisfy(TagChar), (_, c) => 
            (TextUnit)new TextUnit.String(c.ToString()));

    private static readonly Parser<TextUnit> normalFragment =
        from s in Many1Satisfy(NotTagOrEscapeChar)
        select (TextUnit) new TextUnit.String(s);

    private static readonly Parser<TextUnit> tagFragment =
        Between(TAG_OPEN, Many1Satisfy(NotTagChar), TAG_CLOSE).FMap(s => {
            var contInd = -1;
            if (s[0] == TAG_CLOSE_PREFIX)
                return (TextUnit)new TextUnit.CloseTag(s.Substring(1));
            else if ((contInd = s.IndexOf(TAG_CONTENT)) > 0)
                return new TextUnit.OpenTag(s.Substring(0, contInd), s.Substring(contInd + 1));
            else
                return new TextUnit.OpenTag(s, null);
        });
        

    private static readonly Parser<List<TextUnit>> speechParser = Choice(
        tagFragment,
        normalFragment,
        escapedFragment
    ).Many1();

    public static Errorable<List<TextUnit>> Parse(string raw) {
        var res = speechParser(new InputStream("Dialogue text", raw, null!));
        return res.Result.Try(out var v) ?
            Errorable<List<TextUnit>>.OK(v) :
            Errorable<List<TextUnit>>.Fail(res.Error?.Show(raw) ?? "Parsing failed, but no error string is present.");
    }
}
}