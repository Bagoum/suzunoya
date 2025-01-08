using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Suzunoya.Data {
/// <summary>
/// Helpers for JSON serialization.
/// </summary>
public static class Serialization {
    //Supports serializing private getters/setters
    private class NonPublicPropertyResolver : DefaultContractResolver {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            var prop = base.CreateProperty(member, memberSerialization);
            if (member is PropertyInfo pi) {
                prop.Readable = pi.GetMethod != null;
                prop.Writable = pi.SetMethod != null;
            }
            return prop;
        }
    }
    
    /// <summary>
    /// Default JSON serialization settings, supporting polymorphic serialization,
    ///  nonpublic getters/setters, and nonpublic default constructor.
    /// </summary>
    public static readonly JsonSerializerSettings JsonSettings = new() {
        //NB: TypeNameHandling is not supported in System.Text.JSON as of .NET9 for security reasons
        // and requires opt-in via [JsonDerivedType(typeof(Derived), nameof(Derived))] attribute on base class.
        //See https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism ,
        // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft
        TypeNameHandling = TypeNameHandling.Auto,
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        ContractResolver = new NonPublicPropertyResolver()
    };
    
    /// <inheritdoc cref="JsonConvert.SerializeObject(object?,Type?,Formatting,JsonSerializerSettings?)"/>
    public static string SerializeJson<T>(this T obj, Formatting f = Formatting.Indented) =>
        JsonConvert.SerializeObject(obj, typeof(T), f, JsonSettings);

    /// <inheritdoc cref="JsonConvert.DeserializeObject(string,JsonSerializerSettings?)"/>
    public static T? DeserializeJson<T>(this string data) =>
        JsonConvert.DeserializeObject<T>(data, JsonSettings);
}
}