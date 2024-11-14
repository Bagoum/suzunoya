using System.Collections.Generic;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures;

/// <summary>
/// Cache for dictionaries.
/// </summary>
public static class DictCache<K, V> {
    private static readonly Stack<Dictionary<K, V>> cached = new();

    /// <summary>
    /// Return an object to the cache.
    /// </summary>
    public static void Consign(Dictionary<K, V> cacheMe) {
        cacheMe.Clear();
        cached.Push(cacheMe);
    }

    /// <summary>
    /// Get a new object from the cache, or create it if the cache is empty.
    /// </summary>
    public static Dictionary<K, V> Get() => cached.Count > 0 ? cached.Pop() : new Dictionary<K, V>();
}

/// <summary>
/// Cache for lists.
/// </summary>
public static class ListCache<T> {
    private static readonly Stack<List<T>> cached = new();

    /// <summary>
    /// Return an object to the cache.
    /// </summary>
    public static void Consign(List<T> cacheMe) {
        cacheMe.Clear();
        cached.Push(cacheMe);
    }

    /// <summary>
    /// Get a new object from the cache, or create it if the cache is empty.
    /// </summary>
    public static List<T> Get() => cached.Count > 0 ? cached.Pop() : new List<T>();
}

/// <summary>
/// Extensions for <see cref="ListCache{T}"/>, <see cref="DictCache{K,V}"/>, etc.
/// </summary>
[PublicAPI]
public static class CacheExtensions {
    /// <inheritdoc cref="ListCache{T}.Consign"/>
    public static void Consign<T>(this List<T> cacheMe) => ListCache<T>.Consign(cacheMe);
    
    /// <inheritdoc cref="DictCache{K,V}.Consign"/>
    public static void Consign<K,V>(this Dictionary<K,V> cacheMe) => DictCache<K,V>.Consign(cacheMe);
    
    /// <summary>
    /// Set a value in a nested dictionary.
    /// </summary>
    public static void SetDefaultSet<K, K2, V>(this Dictionary<K, Dictionary<K2, V>> dict, K key, K2 key2, V value) {
        if (!dict.TryGetValue(key, out var data))
            data = dict[key] = DictCache<K2, V>.Get();
        data[key2] = value;
    }

    /// <summary>
    /// Consign all lists within this dictionary, and then the dictionary itself.
    /// </summary>
    public static void ConsignRecursive<K, V>(this Dictionary<K, List<V>> dict) {
        foreach (var v in dict.Values)
            ListCache<V>.Consign(v);
        DictCache<K, List<V>>.Consign(dict);
    }
}