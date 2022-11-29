using System;
using System.Reflection;
using Newtonsoft.Json;

namespace BagoumLib.Events {
/// <summary>
/// Newtonsoft.Json serializer for <see cref="Evented{T}"/>
/// </summary>
public class EventedSerializer : JsonConverter  {
    private object? GetEventedValue(object? ev, Type? evType=null) {
        if (ev == null) return null;
        evType ??= ev.GetType();
        return evType.GetProperty("Value")!.GetValue(ev);

    }
    
    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        serializer.Serialize(writer, GetEventedValue(value));
    }

    /// <inheritdoc />
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        var val = serializer.Deserialize(reader, objectType.GetGenericArguments()[0]);
        if (existingValue != null) {
            objectType.GetProperty("Value")!.SetValue(existingValue, val);
            return existingValue;
        } else
            //using new Evented<T>(T initialValue) constructor
            return Activator.CreateInstance(objectType, val);
    }

    /// <inheritdoc />
    public override bool CanConvert(Type objectType) => objectType.IsGenericType &&
                                                        objectType.GetGenericTypeDefinition() == typeof(Evented<>);
}
}