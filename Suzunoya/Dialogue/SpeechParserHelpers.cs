using System;
using System.Collections.Generic;
using System.Reactive;
using Mizuhashi;
using static Mizuhashi.Combinators;

namespace Suzunoya.Dialogue {
internal static partial class SpeechParser {
    private const char ESCAPER = '\\';
    private const char TAG_OPEN = '<';
    private const char TAG_CLOSE = '>';
    private const char TAG_CONTENT = '=';
    private const char TAG_CLOSE_PREFIX = '/';
}
}