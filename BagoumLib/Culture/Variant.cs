using System.Collections.Generic;
using BagoumLib.Events;

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

public static class Localization {
    public static Evented<string?> Locale { get; set; } = new(null);
}

}