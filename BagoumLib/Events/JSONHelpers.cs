using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Expressions;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using JsonGetTypeName = System.Func<System.Type,Newtonsoft.Json.TypeNameAssemblyFormatHandling,Newtonsoft.Json.Serialization.ISerializationBinder?,string>;

namespace BagoumLib {
/// <summary>
/// Helper utilities for JSON serialization.
/// </summary>
public static class JSONHelpers {
    /// <summary>
    /// Get a type name according to Newtonsoft.Json serialization rules.
    /// </summary>
    public static readonly JsonGetTypeName GetTypeName = (JsonGetTypeName)
        Delegate.CreateDelegate(typeof(JsonGetTypeName), ExFunction.WrapAny(typeof(JsonConverter)
            .Assembly.GetType("Newtonsoft.Json.Utilities.ReflectionUtils")!, "GetTypeName").Mi);

}

/// <summary>
/// Newtonsoft.Json serializer for singleton subclasses of a parent class T.
/// <br/>The singleton for a subclass must be accessible as static property `Subclass.S`,
/// or type T must have a static property SINGLETON_KEY that provides the name of the property.
/// <br/>The singletons are serialized using the local class name (Type.Name).
/// </summary>
[PublicAPI]
public class SingletonConverter<T> : JsonConverter {
    // ReSharper disable once StaticMemberInGenericType
    private static Type[]? _subclasses;
    private static Type[] Subclasses => _subclasses ??=
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a => a.GetTypes())
            .Where(type => type.IsSubclassOf(typeof(T)))
            .ToArray();
    // ReSharper disable once StaticMemberInGenericType
    private static string? _keyProp;
    private static string KeyProp => _keyProp ??=
        (typeof(T).GetProperty("SINGLETON_KEY")?.GetValue(null) as string) ?? "S";

    private string? GetSingletonDesc(object? singleton) => singleton?.GetType().Name;

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        serializer.Serialize(writer, GetSingletonDesc(value));
    }

    /// <inheritdoc />
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        var val = (string?)serializer.Deserialize(reader, typeof(string));
        if (val is null) return null;
        foreach (var sc in Subclasses) {
            if (sc.Name == val)
                return sc.GetProperty(KeyProp)!.GetValue(null);
        }
        throw new Exception($"Couldn't find singleton for type name {val}");
    }

    /// <inheritdoc />
    public override bool CanConvert(Type objectType) => typeof(T).IsAssignableFrom(objectType);
}

/// <summary>
/// JSON converter for dictionaries with non-primitive keys.
/// </summary>
[PublicAPI]
public class ComplexDictKeyConverter<K, V> : JsonConverter where K : notnull {
    /// <inheritdoc />
    public override bool CanConvert(Type objectType) {
        return objectType.FullName == typeof(Dictionary<K, V>).FullName;
    }

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value is null) {
            serializer.Serialize(writer, "null");
            return;
        }

        writer.WriteStartArray();
        foreach (var (k, v) in ((Dictionary<K, V>)value).Items()) {
            writer.WriteStartArray();
            serializer.Serialize(writer, k);
            serializer.Serialize(writer, v);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }

    /// <inheritdoc />
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer) {
        if (objectType.FullName != typeof(Dictionary<K, V>).FullName) {
            throw new NotSupportedException($"{objectType} is not Dictionary<{typeof(K)},{typeof(V)}>");
        }
        return JToken.Load(reader).ToDictionary(t => t.First!.ToObject<K>()!, t => t.Last!.ToObject<V>());
    }
}
}

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