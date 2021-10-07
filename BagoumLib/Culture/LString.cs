using System;
using System.Linq;

namespace BagoumLib.Culture {

public class LString : Variant<string> {
    
    public static LString Empty => new LString("");
    public LString(string defaultValue, params (string locale, string value)[] variants) : base(defaultValue, variants) { }

    public override string ToString() => Value;

    public static implicit operator LString(string s) => new LString(s) { ID = s };
    
    public static string Format(string? locale, string fmtString, params LString[] args) =>
        string.Format(fmtString, args.Select(s => (object) s.Realize(locale)).ToArray());
    
    public static LString Format(string fmtString, params LString[] args) =>
        Format(new LString(fmtString), args);

    public static LString Format(LString fmtString, params LString[] args) => 
        new(Format(null, fmtString.defaultValue, args),
            args.Append(fmtString)
                .SelectMany(l => l.langToValueMap.Keys)
                .Distinct()
                .Select(lang => (lang, Format(lang, fmtString.Realize(lang), args)))
                .ToArray());

    public LString Or(LString second) => new(defaultValue.Or(second.defaultValue),
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
        new(mapper(null, defaultValue), langToValueMap.Select(x => (x.Key, mapper(x.Key, x.Value))).ToArray());
}

}