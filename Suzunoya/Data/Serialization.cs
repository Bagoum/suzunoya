using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Suzunoya.Data {
public static class Serialization {
    //To support private setters
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
    
    public static readonly JsonSerializerSettings JsonSettings = new() {
        TypeNameHandling = TypeNameHandling.Auto,
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        ContractResolver = new NonPublicPropertyResolver()
    };
    
    public static string SerializeJson<T>(T obj, Formatting f = Formatting.Indented) =>
        JsonConvert.SerializeObject(obj, typeof(T), f, JsonSettings);

    public static T? DeserializeJson<T>(string data) =>
        JsonConvert.DeserializeObject<T>(data, JsonSettings);
}
}