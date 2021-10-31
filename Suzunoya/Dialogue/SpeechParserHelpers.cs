using System;
using System.Collections.Generic;
using System.Reactive;
using Mizuhashi;
using static Mizuhashi.Combinators;

namespace Suzunoya.Dialogue {
public static partial class SpeechParser {
    public const char NEWLINE = '\n';
    public const char ESCAPER = '\\';
    public const char TAG_OPEN = '<';
    public const char TAG_CLOSE = '>';
    public const char TAG_CONTENT = '=';
    public const char TAG_CLOSE_PREFIX = '/';
}
}