using System.Collections.Generic;
using BagoumLib.Events;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace BagoumLib.Culture {

/// <summary>
/// Interface for an object whose value changes based on the current language,
///  as provided by an <see cref="ILocaleProvider"/>.
/// </summary>
public interface IVariant {
    string? ID { get; }
    object RealizeObj(string? locale);
}
/// <summary>
/// An <see cref="IVariant{T}"/> whose object type is constrained.
/// </summary>
/// <typeparam name="T">Type of varying object</typeparam>
public interface IVariant<T> : IVariant {
    T Realize(string? locale);
}

/// <summary>
/// Provides a language that can be interpreted by <see cref="IVariant{T}"/>.
/// </summary>
public interface ILocaleProvider {
    public Evented<string?> LocaleEv { get; }
    public string? Locale => LocaleEv.Value;
}

/// <summary>
/// Baseline implementation of <see cref="IVariant{T}"/>.
/// </summary>
[PublicAPI]
public class Variant<T> : IVariant<T> {
    public string? ID { get; set; } = null;
    public readonly T defaultValue;
    [JsonProperty] private readonly (string, T)[] variants;
    protected readonly Dictionary<string, T> langToValueMap = new();
    protected readonly ILocaleProvider? localeP;
    
    [JsonIgnore] public T Value => Realize(localeP?.Locale);

    public Variant(ILocaleProvider? localeP, T defaultValue, params (string locale, T value)[] variants) {
        this.variants = variants;
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

/// <summary>
/// Baseline implementation of <see cref="ILocaleProvider"/>.
/// </summary>
public record LocaleProvider(Evented<string?> LocaleEv) : ILocaleProvider;

}