using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Suzunoya.Data {

/// <summary>
/// Interface for a basic key-value repository for save data.
/// </summary>
public interface IKeyValueRepository {
    /// <summary>
    /// Saves a key-value pair in the repository. Overwrites if existing. Noop if key is null.
    /// </summary>
    void SaveData<T>(string? key, T value);
    /// <summary>
    /// Retrieve a value from the repository. Throws if nonexistent or key is null.
    /// </summary>
    T GetData<T>(string? key);
    /// <summary>
    /// Check if a key exists in the repository. False if key is null.
    /// </summary>
    bool HasData(string? key);
    /// <summary>
    /// Get all keys saved in this repository.
    /// </summary>
    IEnumerable<string> Keys { get; }
}
//You can't proto-compress object, so json it is!
/// <summary>
/// Basic implementation of <see cref="IKeyValueRepository"/>.
/// </summary>
[Serializable]
public class KeyValueRepository : IKeyValueRepository {
    /// <summary>
    /// Underlying data store.
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = new();
    /// <inheritdoc/>
    [JsonIgnore]
    public IEnumerable<string> Keys => Data.Keys;

    /// <inheritdoc/>
    public void SaveData<T>(string? key, T value) {
        if (key == null) return;
        if (value == null) throw new Exception("Cannot save null values. Use Maybe<T> instead");
        Data[key] = value;
    }

    /// <inheritdoc/>
    public T GetData<T>(string? key) {
        if (key == null)
            throw new Exception("Null keys are not permitted in the KVR.");
        if (!Data.TryGetValue(key, out var value))
            throw new Exception($"Could not find data by key {key}");
        var cast = (T) value;
        if (cast == null) throw new Exception($"Value loaded for {key} is null");
        return cast;
    }

    /// <inheritdoc/>
    public bool HasData(string? key) => key != null && Data.ContainsKey(key);
}
}