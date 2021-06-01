using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BagoumLib;
using BagoumLib.Events;
using JetBrains.Annotations;

namespace BagoumLib.Culture {
public interface IVariant {
    string? ID { get; }
    object RealizeObj(string? locale);
}
public interface IVariant<T> : IVariant {
    T Realize(string? locale);
}

public class Variant<T> : IVariant<T> {
    //Should be init instead of set, but framework472 causes issues with that.
    public string? ID { get; set; } = null;
    protected readonly T defaultValue;
    protected readonly Dictionary<string, T> langToValueMap = new();
    
    public T Value => Realize(Localization.Locale);

    public Variant(T defaultValue, params (string locale, T value)[] variants) {
        this.defaultValue = defaultValue;
        foreach (var (locale, value) in variants) {
            langToValueMap[locale] = value;
        }
    }

    public bool HasLang(string lang) => langToValueMap.ContainsKey(lang);

    public T Realize(string? locale) => 
        locale != null && langToValueMap.TryGetValue(locale, out var val) ? val : defaultValue;

    public object RealizeObj(string? locale) => Realize(locale)!;

    public static implicit operator T(Variant<T> variant) => variant.Value;
}

public class LString : Variant<string> {
    
    public static LString Empty => new LString("");
    public LString(string defaultValue, params (string locale, string value)[] variants) : base(defaultValue, variants) { }

    public override string ToString() => Value;

    public static implicit operator LString(string s) => new LString(s);
    
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

public static class Localization {
    public static Evented<string?> Locale { get; set; } = new(null);
}

}