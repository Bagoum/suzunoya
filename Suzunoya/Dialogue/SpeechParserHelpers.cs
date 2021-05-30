using System;
using System.Collections.Generic;
using LanguageExt;
using LanguageExt.Parsec;
using static LanguageExt.Prelude;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Parsec.Char;
using static LanguageExt.Parsec.Expr;
using static LanguageExt.Parsec.Token;

namespace Suzunoya.Dialogue {
public static partial class SpeechParser {
    public const char NEWLINE = '\n';
    public const char ESCAPER = '\\';
    public const char TAG_OPEN = '<';
    public const char TAG_CLOSE = '>';
    public const char TAG_CONTENT = '=';
    public const char TAG_CLOSE_PREFIX = '/';

    public static Parser<T> BetweenChars<T>(char c1, char c2, Parser<T> p) =>
        Sequential(ch(c1), p, ch(c2), (_, x, __) => x);

    public static Parser<T> BetweenStrs<T>(string c1, string c2, Parser<T> p) =>
        Sequential(PString(c1), p, PString(c2), (_, x, __) => x);

    public static Parser<string> Bounded(char c) =>
        BetweenChars(c, c, manyString(satisfy(x => x != c)));

    public static bool WhiteInline(char c) => c != NEWLINE && char.IsWhiteSpace(c);
    
    public static Parser<Unit> ILSpaces = skipMany(satisfy(WhiteInline));
    public static Parser<Unit> ILSpaces1 = skipMany1(satisfy(WhiteInline));
    public static Parser<Unit> Spaces1 = skipMany1(space);
}
}