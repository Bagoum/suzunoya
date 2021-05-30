using System;
using System.Collections.Generic;
using BagoumLib.Functional;
using LanguageExt;
using LanguageExt.Parsec;
using static LanguageExt.Prelude;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Parsec.Char;
using static LanguageExt.Parsec.Expr;
using static LanguageExt.Parsec.Token;

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

    private static readonly Parser<TextUnit> escapedFragment =
        Sequential(ch(ESCAPER), satisfy(TagChar), (_, c) => 
            (TextUnit)new TextUnit.String(c.ToString()));

    private static readonly Parser<TextUnit> normalFragment =
        from s in many1String(satisfy(NotTagChar))
        select (TextUnit) new TextUnit.String(s);

    private static readonly Parser<TextUnit> tagFragment =
        BetweenChars(TAG_OPEN, TAG_CLOSE, many1String(satisfy(NotTagChar))).Map(s => {
            var contInd = -1;
            if (s[0] == TAG_CLOSE_PREFIX)
                return (TextUnit)new TextUnit.CloseTag(s.Substring(1));
            else if ((contInd = s.IndexOf(TAG_CONTENT)) > 0)
                return new TextUnit.OpenTag(s.Substring(0, contInd), s.Substring(contInd + 1));
            else
                return new TextUnit.OpenTag(s, null);
        });
        

    private static readonly Parser<List<TextUnit>> speechParser = many1(choice(
        tagFragment,
        normalFragment,
        escapedFragment
    ));

    public static Errorable<List<TextUnit>> Parse(string raw) {
        var res = parse(speechParser, raw);
        return res.IsFaulted ?
            Errorable<List<TextUnit>>.Fail(res.Reply.Error.ToString()) :
            Errorable<List<TextUnit>>.OK(res.Reply.Result);
    }
}
}