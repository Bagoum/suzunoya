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

public interface ILocaleProvider {
    public Evented<string?> LocaleEv { get; }
    public string? Locale => LocaleEv.Value;
}

public class Variant<T> : IVariant<T> {
    public string? ID { get; set; } = null;
    public readonly T defaultValue;
    protected readonly Dictionary<string, T> langToValueMap = new();
    protected readonly ILocaleProvider? localeP;
    
    public T Value => Realize(localeP?.Locale);

    public Variant(ILocaleProvider? localeP, T defaultValue, params (string locale, T value)[] variants) {
        this.localeP = localeP;
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

public record LocaleProvider(Evented<string?> LocaleEv) : ILocaleProvider;

}