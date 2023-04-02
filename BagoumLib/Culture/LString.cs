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
    
    /// <inheritdoc cref="LString"/>
    public LString(ILocaleProvider? provider, string defaultValue, 
        params (string locale, string value)[] variants) : base(provider, defaultValue, variants) { }

    /// <inheritdoc cref="LString"/>
    public override string ToString() => Value;

    /// <summary>
    /// Wrap a string with no variation functionality.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static implicit operator LString(string s) => new LString(null, s) { ID = s };

    private static ILocaleProvider? FirstProvider(LString? a, IReadOnlyList<LString> lis) {
        if (a?.localeP != null)
            return a.localeP;
        foreach (var l in lis)
            if (l.localeP != null)
                return l.localeP;
        return null;
    }
    
    /// <summary>
    /// Realize a format string and all of its arguments for a given locale, then combine them with string.Format.
    /// </summary>
    public static string FormatLocale(string? locale, LString fmtString, params LString[] args) =>
        string.Format(fmtString.Realize(locale), args.Select(s => (object) s.Realize(locale)).ToArray());

    /// <summary>
    /// For each locale that has value for any of the arguments, run <see cref="FormatLocale"/> over that locale,
    ///  then combine all the results into a single <see cref="LString"/>.
    /// </summary>
    public static LString Format(LString fmtString, params LString[] args) => 
        new(FirstProvider(fmtString, args),
            FormatLocale(null, fmtString, args),
            args.Append(fmtString)
                .SelectMany(l => l.langToValueMap.Keys)
                .Distinct()
                .Select(lang => (lang, FormatLocale(lang, fmtString, args)))
                .ToArray());

    /// <summary>
    /// Realize all arguments for a given locale, then combine them with the provided formatter.
    /// </summary>
    public static string FormatFnLocale(string? locale, Func<string[], string> formatter, 
        params LString[] args) => formatter(args.Select(s => s.Realize(locale)).ToArray());
    
    /// <summary>
    /// For each locale that has value for any of the arguments, run <see cref="FormatFnLocale"/> over that locale,
    ///  then combine all the results into a single <see cref="LString"/>.
    /// </summary>
    public static LString FormatFn(Func<string[], string> formatter, params LString[] args) =>
        new(FirstProvider(null, args),
            FormatFnLocale(null, formatter, args),
            args.SelectMany(l => l.langToValueMap.Keys)
                .Distinct()
                .Select(lang => (lang, FormatFnLocale(lang, formatter, args)))
                .ToArray());

    /// <summary>
    /// For each locale that has value in `this` or `other`, create a realization selecting `this.Realize(locale)`,
    ///  or if it is null or empty, then `second.Realize(locale)` instead.
    /// </summary>
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
    /// Run all variant values through a mapping function, and return a new <see cref="LString"/>.
    /// </summary>
    /// <param name="mapper">Locale -> String -> New String</param>
    public LString FMap(Func<string?, string, string> mapper) => 
        new(localeP, mapper(null, defaultValue), 
            langToValueMap.Select(x => (x.Key, mapper(x.Key, x.Value))).ToArray());
}

}