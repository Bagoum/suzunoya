using System;
using Newtonsoft.Json;

namespace Suzunoya.Data {
public static class Serialization {
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings() {
        TypeNameHandling = TypeNameHandling.Auto,
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
    };
    
    public static string SerializeJson(object obj) => 
        JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSettings);

    public static T? DeserializeJson<T>(string serialized) => 
        JsonConvert.DeserializeObject<T>(serialized, JsonSettings);
}
}