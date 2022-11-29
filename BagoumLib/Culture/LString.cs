using System;
using System.Collections.Generic;
using System.Linq;

namespace BagoumLib.Culture {

/// <summary>
/// <see cref="Variant{T}"/> with some utility functions specific to strings.
/// </summary>
public class LString : Variant<string> {
    /// <summary>
    /// Default empty string.
    /// </summary>
    public static LString Empty => new LString(null, "");
    public LString(ILocaleProvider? provider, string defaultValue, 
        params (string locale, string value)[] variants) : base(provider, defaultValue, variants) { }

    public override string ToString() => Value;

    public static implicit operator LString(string s) => new LString(null, s) { ID = s };

    private static ILocaleProvider? FirstProvider(LString? a, IReadOnlyList<LString> lis) {
        if (a?.localeP != null)
            return a.localeP;
        foreach (var l in lis)
            if (l.localeP != null)
                return l.localeP;
        return null;
    }
    
    public static string FormatLocale(string? locale, LString fmtString, params LString[] args) =>
        string.Format(fmtString.Realize(locale), args.Select(s => (object) s.Realize(locale)).ToArray());

    public static LString Format(LString fmtString, params LString[] args) => 
        new(FirstProvider(fmtString, args),
            FormatLocale(null, fmtString, args),
            args.Append(fmtString)
                .SelectMany(l => l.langToValueMap.Keys)
                .Distinct()
                .Select(lang => (lang, FormatLocale(lang, fmtString, args)))
                .ToArray());

    public static string FormatFnLocale(string? locale, Func<string[], string> formatter, 
        params LString[] args) => formatter(args.Select(s => s.Realize(locale)).ToArray());
    
    public static LString FormatFn(Func<string[], string> formatter, params LString[] args) =>
        new(FirstProvider(null, args),
            FormatFnLocale(null, formatter, args),
            args.SelectMany(l => l.langToValueMap.Keys)
                .Distinct()
                .Select(lang => (lang, FormatFnLocale(lang, formatter, args)))
                .ToArray());

    public LString Or(LString second) => new(localeP ?? second.localeP, 
        defaultValue.Or(second.defaultValue),
        new[] {this, second}
            .SelectMany(l => l.langToValueMap.Keys)
            .Distinct()
            .Select(lang => {
                var val = HasLang(lang) ?
                    second.HasLang(lang) ?
                        Realize(lang).Or(second.Realize(lang)) :
                        Realize(lang) :
                    defaultValue.Or(second.Realize(lang));
                return (lang, val);
            }).ToArray()
    );
    
    /// <summary>
    /// </summary>
    /// <param name="mapper">Locale -> String -> New String</param>
    /// <returns></returns>
    public LString FMap(Func<string?, string, string> mapper) => 
        new(localeP, mapper(null, defaultValue), 
            langToValueMap.Select(x => (x.Key, mapper(x.Key, x.Value))).ToArray());
}

}