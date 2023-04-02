using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// A dictionary where the set of keys is stored separately, allowing zero-garbage iteration.
/// </summary>
[PublicAPI]
public class DictionaryWithKeys<K, V> where K : notnull {
    private readonly Dictionary<K, V> data = new(4);
    /// <summary>
    /// Dictionary data.
    /// </summary>
    public IReadOnlyDictionary<K, V> Data => data;
    /// <summary>
    /// Array of dictionary keys.
    /// </summary>
    public AbstractDMCompactingArray<K> Keys { get; } = new(4);
    
    /// <summary>
    /// Get/set accessors for the underlying data.
    /// </summary>
    public V this[K key] {
        get => data[key];
        set {
            if (!data.ContainsKey(key))
                Keys.AddToEnd(DWKDeletionMarker.Make(this, key, 0));
            data[key] = value;
        }
    }

    private class DWKDeletionMarker : IDeletionMarker<K> {
        private static readonly Stack<DWKDeletionMarker> cache = new();
        private DictionaryWithKeys<K, V> container;
        public int Priority { get; private set; }
        public K Value { get; private set; }
        bool IDeletionMarker.MarkedForDeletion => MarkedForDeletion;
        private bool MarkedForDeletion { get; set; }
        
        private DWKDeletionMarker(DictionaryWithKeys<K, V> container, K value, int priority) {
            this.container = container;
            this.Value = value;
            this.Priority = priority;
        }

        public static DWKDeletionMarker Make(DictionaryWithKeys<K, V> container, K value, int priority) {
            if (cache.TryPop(out var dm)) {
                dm.container = container;
                dm.Value = value;
                dm.Priority = priority;
                dm.MarkedForDeletion = false;
                return dm;
            } else
                return new(container, value, priority);
        }

        public void MarkForDeletion() {
            container.data.Remove(Value);
            MarkedForDeletion = true;
        }

        public void RemovedFromCollection() {
            Value = default!;
            cache.Push(this);
        }
    }
}
}