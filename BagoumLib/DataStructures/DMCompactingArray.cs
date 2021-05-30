using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
[PublicAPI]
public interface IDeletionMarker : IDisposable {
    void MarkForDeletion();
}

public class DeletionMarker<T> : IDeletionMarker {
    public T Value { get; }
    public int Priority { get; }
    public bool MarkedForDeletion { get; private set; } = false;

    public DeletionMarker(T value, int priority) {
        this.Value = value;
        this.Priority = priority;
    }

    public void MarkForDeletion() => MarkedForDeletion = true;
    public void Dispose() => MarkForDeletion();
}

/// <summary>
/// An ordered collection that supports iteration, as well as deletion of arbitrary elements via
/// persistent identification of added elements returned to consumers.
/// Indices are not guaranteed to be persistent and should not be used for identification.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class DMCompactingArray<T> : IEnumerable<T> {
    private int count;
    public int Count => count;
    public DeletionMarker<T>[] Data { get; private set; }

    public DMCompactingArray(int size = 8) {
        Data = new DeletionMarker<T>[size];
        count = 0;
    }

    public void Compact() {
        int ii = 0;
        bool foundDeleted = false;
        while (ii < count) {
            if (Data[ii++].MarkedForDeletion) {
                foundDeleted = true;
                break;
            }
        }
        if (!foundDeleted) return;
        int deficit = 1;
        int start_copy = ii;
        for (; ii < count; ++ii) {
            if (Data[ii].MarkedForDeletion) {
                if (ii > start_copy) {
                    Array.Copy(Data, start_copy,
                        Data, start_copy - deficit, ii - start_copy);
                }
                ++deficit;
                start_copy = ii + 1;
            }
        }
        Array.Copy(Data, start_copy,
            Data, start_copy - deficit, count - start_copy);
        count -= deficit;
    }

    private void MaybeResize() {
        if (count >= Data.Length) {
            var narr = new DeletionMarker<T>[Data.Length * 2];
            Data.CopyTo(narr, 0);
            Data = narr;
        }
    }

    public DeletionMarker<T> Add(T obj) {
        MaybeResize();
        var dm = new DeletionMarker<T>(obj, 0);
        Data[count++] = dm;
        return dm;
    }

    /// <summary>
    /// Returns the first index where the priority is greater than the given value.
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    private int NextIndexForPriority(int p) {
        //TODO you can make this binary search or whatever
        for (int ii = 0; ii < count; ++ii) {
            if (Data[ii].Priority > p) return ii;
        }
        return count;
    }

    public int FirstPriorityGT(int i) => NextIndexForPriority(i - 1);

    /// <summary>
    /// Add an element into the array with a priority.
    /// Lower priorities will be inserted at the front of the array.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="priority"></param>
    /// <returns></returns>
    public DeletionMarker<T> AddPriority(T obj, int priority) {
        var dm = new DeletionMarker<T>(obj, priority);
        AddPriority(dm);
        return dm;
    }

    public void AddPriority(DeletionMarker<T> dm) {
        MaybeResize();
        Data.Insert(ref count, dm, NextIndexForPriority(dm.Priority));
    }

    public void Empty() {
        for (int ii = 0; ii < count; ++ii) {
            Data[ii] = null!;
        }
        count = 0;
    }

    public void Delete(int ii) => Data[ii].MarkForDeletion();

    public bool ExistsAt(int index) => !Data[index].MarkedForDeletion;
    public T this[int index] => Data[index].Value;

    public IEnumerator<T> GetEnumerator() {
        int ct = count;
        for (int ii = 0; ii < ct; ++ii) {
            if (!Data[ii].MarkedForDeletion) yield return Data[ii].Value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
}