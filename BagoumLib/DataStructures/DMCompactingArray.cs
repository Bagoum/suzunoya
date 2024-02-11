using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// A disposable marker. When disposed, some underlying object will be removed from a collection.
/// </summary>
[PublicAPI]
public interface IDeletionMarker : IDisposable {
    /// <summary>
    /// Sorting priority in the array.
    /// </summary>
    int Priority { get; }
    /// <summary>
    /// Whether or not the element is currently marked as deleted.
    /// </summary>
    internal bool MarkedForDeletion { get; }

    /// <summary>
    /// Mark the element as deleted. The caller should stop using the marker after this.
    /// <br/>It is unsafe to reuse (eg. via caching) deletion markers at this point, as the collection
    ///  may not have deleted them. Wait until <see cref="RemovedFromCollection"/> is called to cache deletion markers.
    /// </summary>
    void MarkForDeletion();
    void IDisposable.Dispose() => MarkForDeletion();

    /// <summary>
    /// Called when the collection removes the marker and the underlying element.
    /// <br/>It is *not guaranteed* that this function will be called, so you should not depend on it.
    /// <br/>It is permissible to reuse (eg. via caching) deletion markers when this is called,
    ///  as long as you are not using the marker somewhere else.
    /// </summary>
    void RemovedFromCollection() { }
}

/// <summary>
/// An <see cref="IDeletionMarker"/> for an underlying value of type <see cref="T"/>.
/// </summary>
public interface IDeletionMarker<T> : IDeletionMarker {
    /// <summary>
    /// Value of the object in the collection.
    /// </summary>
    public T Value { get; }
}

/// <summary>
/// A disposable marker for a value <see cref="Value"/> within a collection (see <see cref="DMCompactingArray{T}"/>).
/// </summary>
public sealed class DeletionMarker<T> : IDeletionMarker<T> {
    private static readonly Stack<DeletionMarker<T>> cache = new();
    /// <inheritdoc/>
    public int Priority { get; private set; }
    bool IDeletionMarker.MarkedForDeletion => MarkedForDeletion;
    internal bool MarkedForDeletion { get; private set; }
    private bool poolingAllowed = false;
    /// <summary>
    /// Value of the object in the array.
    /// </summary>
    public T Value;

    T IDeletionMarker<T>.Value => Value;

    /// <summary>
    /// Create a new deletion marker with the given value and priority.
    /// </summary>
    private DeletionMarker(T value, int priority) {
        this.Value = value;
        this.Priority = priority;
    }

    /// <summary>
    /// Create a new deletion marker with the given value and priority.
    /// </summary>
    public static DeletionMarker<T> Make(T value, int priority) {
        if (cache.TryPop(out var dm)) {
            dm.Value = value;
            dm.Priority = priority;
            dm.MarkedForDeletion = false;
            dm.poolingAllowed = false;
            return dm;
        } else
            return new(value, priority);
    }

    /// <inheritdoc/>
    public void MarkForDeletion() => MarkedForDeletion = true;

    /// <inheritdoc/>
    public void RemovedFromCollection() {
        Value = default!;
        if (poolingAllowed)
            cache.Push(this);
    }

    /// <summary>
    /// Mark that a DeletionMarker is safe to be pooled when it receives <see cref="RemovedFromCollection"/>.
    /// <br/>Call this on construction if you are not using the DeletionMarker anywhere else.
    /// </summary>
    public DeletionMarker<T> AllowPooling() {
        poolingAllowed = true;
        return this;
    }

    /// <summary>
    /// Mark that a DeletionMarker is unsafe to be pooled.
    /// </summary>
    public DeletionMarker<T> DisallowPooling() {
        poolingAllowed = false;
        return this;
    }

    /// <inheritdoc/>
    public override string ToString() {
        var del = MarkedForDeletion ? "[Deleted] " : "";
        return $"{del} {Value?.ToString()} (P:{Priority})";
    }
}

/// <summary>
/// An ordered collection that supports iteration, as well as deletion of arbitrary elements via
/// disposable tokens (<see cref="DeletionMarker{T}"/>) returned to consumers.
/// Indices are not guaranteed to be persistent and should not be used for identification.
/// <br/>Deletion is O(1) amortized, assuming that <see cref="AnyTypeDMCompactingArray{D}.Compact"/> is called at a reasonable frequency.
/// </summary>
[PublicAPI]
public class DMCompactingArray<T> : AnyTypeDMCompactingArray<DeletionMarker<T>>, IReadOnlyDMCompactingArray<T> {
    /// <summary>
    /// A statically-shared empty array.
    /// </summary>
    public static IReadOnlyDMCompactingArray<T> EmptyArray { get; } = new DMCompactingArray<T>(1);

    /// <summary>
    /// Create a new compacting array with the provided initial capacity.
    /// </summary>
    /// <param name="capacity"></param>
    public DMCompactingArray(int capacity = 8) : base(capacity) { }
    
    /// <summary>
    /// Add an element to the array with a default ordering priority of zero.
    /// </summary>
    public DeletionMarker<T> Add(T obj) {
        MaybeResize();
        var dm = DeletionMarker<T>.Make(obj, 0);
        Data[count++] = dm;
        return dm;
    }
    /// <summary>
    /// Add an element into the array with a priority.
    /// Lower priorities will be inserted at the front of the array.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="priority"></param>
    /// <returns></returns>
    public DeletionMarker<T> AddPriority(T obj, int priority) {
        var dm = DeletionMarker<T>.Make(obj, priority);
        AddPriority(dm);
        return dm;
    }
    
    /// <summary>
    /// Get the index'th element in the array. Note that this does not check if the element has been deleted.
    /// </summary>
    public ref T this[int index] => ref Data[index].Value;

    /// <inheritdoc cref="IReadOnlyDMCompactingArray{T}.GetIfExistsAt"/>
    public bool GetIfExistsAt(int index, out T val) {
        if (Data[index].MarkedForDeletion) {
            val = default!;
            return false;
        } else {
            val = Data[index].Value;
            return true;
        }
    }
    
    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() {
        for (int ii = 0; ii < count; ++ii)
            if (!Data[ii].MarkedForDeletion) 
                yield return Data[ii].Value;
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    /// <summary>
    /// Copy the non-deleted elements of this array into a list.
    /// </summary>
    public void CopyIntoList(List<T> dst) {
        for (int ii = 0; ii < count; ++ii)
            if (!Data[ii].MarkedForDeletion)
                dst.Add(Data[ii].Value);
    }

    /// <summary>
    /// Get the first element in the array, or null if it is empty.
    /// </summary>
    public T? FirstOrNull() {
        for (int ii = 0; ii < count; ++ii)
            if (!Data[ii].MarkedForDeletion)
                return Data[ii].Value;
        return default(T?);
    }
    
    /// <summary>
    /// Get the first element in the array, or None if it is empty.
    /// </summary>
    public Maybe<T> FirstOrNone() {
        for (int ii = 0; ii < count; ++ii)
            if (!Data[ii].MarkedForDeletion)
                return Data[ii].Value;
        return Maybe<T>.None;
    }
}
}