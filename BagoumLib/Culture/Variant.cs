using System.Collections.Generic;
using BagoumLib.Events;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace BagoumLib.Culture {

/// <summary>
/// Interface for an object whose value changes based on the current locale,
///  as provided by an <see cref="ILocaleProvider"/>.
/// </summary>
public interface IVariant {
    /// <summary>
    /// An identifier for the object that may be used to key it in maps. Not required if no such usages occur.
    /// </summary>
    string? ID { get; }
    /// <summary>
    /// Get the value of the object in the given locale.
    /// </summary>
    object RealizeObj(string? locale);
}
/// <summary>
/// An <see cref="IVariant{T}"/> whose object type is constrained.
/// </summary>
/// <typeparam name="T">Type of varying object</typeparam>
public interface IVariant<T> : IVariant {
    /// <inheritdoc cref="IVariant.RealizeObj"/>
    T Realize(string? locale);
}

/// <summary>
/// Provides a language that can be interpreted by <see cref="IVariant{T}"/>.
/// </summary>
public interface ILocaleProvider {
    /// <summary>
    /// An event defining the current locale.
    /// </summary>
    public Evented<string?> LocaleEv { get; }
    /// <summary>
    /// The current locale.
    /// </summary>
    public string? Locale => LocaleEv.Value;
}

/// <summary>
/// Baseline implementation of <see cref="IVariant{T}"/>.
/// </summary>
[PublicAPI]
public class Variant<T> : IVariant<T> {
    /// <inheritdoc />
    public string? ID { get; set; } = null;
    /// <summary>
    /// Default value to realize if the locale is null or there is no handling for the provided locale.
    /// </summary>
    public readonly T defaultValue;
    [JsonProperty] private readonly (string, T)[] variants;
    /// <summary>
    /// A map from locales to the object value for that locale.
    /// </summary>
    protected readonly Dictionary<string, T> langToValueMap = new();
    /// <summary>
    /// The <see cref="ILocaleProvider"/> used to get the current locale.
    /// </summary>
    protected readonly ILocaleProvider? localeP;
    
    /// <summary>
    /// The current value of the locale-variant object, with the locale defined by <see cref="localeP"/>
    ///  (or defaulting to null if no locale provider is set).
    /// </summary>
    [JsonIgnore] public T Value => Realize(localeP?.Locale);

    /// <summary>
    /// Create a locale-variant object.
    /// </summary>
    public Variant(ILocaleProvider? localeP, T defaultValue, params (string locale, T value)[] variants) {
        this.variants = variants;
        this.localeP = localeP;
        this.defaultValue = defaultValue;
        foreach (var (locale, value) in variants) {
            langToValueMap[locale] = value;
        }
    }

    /// <summary>
    /// Whether or not there is handling for the provided locale.
    /// </summary>
    public bool HasLang(string lang) => langToValueMap.ContainsKey(lang);

    /// <inheritdoc/>
    public T Realize(string? locale) => 
        locale != null && langToValueMap.TryGetValue(locale, out var val) ? val : defaultValue;

    /// <inheritdoc/>
    public object RealizeObj(string? locale) => Realize(locale)!;

    /// <summary>
    /// <see cref="Variant{T}.Value"/>
    /// </summary>
    public static implicit operator T(Variant<T> variant) => variant.Value;
}

/// <summary>
/// Baseline implementation of <see cref="ILocaleProvider"/>.
/// </summary>
public record LocaleProvider(Evented<string?> LocaleEv) : ILocaleProvider;

}