using System;
using System.Reflection;
using Newtonsoft.Json;

namespace BagoumLib.Events {
public class EventedSerializer : JsonConverter  {
    private object? GetEventedValue(object? ev, Type? evType=null) {
        if (ev == null) return null;
        evType ??= ev.GetType();
        return evType.GetProperty("Value")!.GetValue(ev);

    }
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        serializer.Serialize(writer, GetEventedValue(value));
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        //using new Evented<T>(T initialValue) constructor
        return Activator.CreateInstance(objectType,
            serializer.Deserialize(reader, objectType.GetGenericArguments()[0]));
    }

    public override bool CanConvert(Type objectType) => objectType.IsGenericType &&
                                                        objectType.GetGenericTypeDefinition() == typeof(Evented<>);
}
}