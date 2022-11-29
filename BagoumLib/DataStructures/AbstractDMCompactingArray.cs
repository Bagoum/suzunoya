using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Functional;

namespace BagoumLib.DataStructures {
/// <summary>
/// Alternate implementation of <see cref="DMCompactingArray{T}"/> which allows multiple concrete implementations of
/// <see cref="IDeletionMarker{T}"/> in one array.
/// </summary>
/// <typeparam name="T">Value type of array elements</typeparam>
public class AbstractDMCompactingArray<T> : AnyTypeDMCompactingArray<IDeletionMarker<T>>, IEnumerable<T> {
    /// <summary>
    /// Create a new compacting array with the provided initial capacity.
    /// </summary>
    /// <param name="capacity"></param>
    public AbstractDMCompactingArray(int capacity = 8) : base(capacity) { }

    /// <inheritdoc cref="DMCompactingArray{T}.Item"/>
    public T this[int index] => Data[index].Value;

    /// <inheritdoc cref="DMCompactingArray{T}.GetIfExistsAt"/>
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

    /// <inheritdoc cref="DMCompactingArray{T}.FirstOrNull"/>
    public T? FirstOrNull() {
        for (int ii = 0; ii < count; ++ii)
            if (!Data[ii].MarkedForDeletion)
                return Data[ii].Value;
        return default(T?);
    }
    
    /// <inheritdoc cref="DMCompactingArray{T}.FirstOrNone"/>
    public Maybe<T> FirstOrNone() {
        for (int ii = 0; ii < count; ++ii)
            if (!Data[ii].MarkedForDeletion)
                return Data[ii].Value;
        return Maybe<T>.None;
    }
    
}
}