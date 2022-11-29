using System;
using System.Collections.Generic;

namespace BagoumLib.DataStructures {
/// <summary>
/// Generalized implementation of <see cref="DMCompactingArray{T}"/> for multiple types of <see cref="DeletionMarker{T}"/> in one array,
///  even if the underlying value types are different.
/// </summary>
/// <typeparam name="D">Subimplementation of <see cref="IDeletionMarker"/></typeparam>
public class AnyTypeDMCompactingArray<D> where D : IDeletionMarker {
    /// <inheritdoc cref="Count"/>
    protected int count;
    /// <summary>
    /// Number of elements present in the array.
    /// Some of the elements may be deleted. Call <see cref="Compact"/> to remove deleted elements and make this number
    ///  stricter.
    /// </summary>
    public int Count => count;
    /// <summary>
    /// Underlying data array.
    /// </summary>
    protected D[] Data { get; private set; }

    /// <summary>
    /// Create a new compacting array with the provided initial capacity.
    /// </summary>
    /// <param name="capacity"></param>
    public AnyTypeDMCompactingArray(int capacity = 8) {
        Data = new D[capacity];
        count = 0;
    }
    

    /// <summary>
    /// Remove deleted elements from the underlying data array.
    /// </summary>
    /// <returns>True iff compaction occurred, false iff the array was not modified.</returns>
    public bool Compact() {
        int ii = 0;
        while (true) {
            if (ii == count)
                return false;
            if (Data[ii++].MarkedForDeletion) {
                Data[ii - 1].RemovedFromCollection();
                break;
            }
        }
        int deficit = 1;
        int start_copy = ii;
        for (; ii < count; ++ii) {
            if (Data[ii].MarkedForDeletion) {
                Data[ii].RemovedFromCollection();
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
        Array.Clear(Data, count, deficit);
        return true;
    }

    /// <summary>
    /// Resize the array if it is full.
    /// </summary>
    protected void MaybeResize() {
        if (count >= Data.Length) {
            var narr = new D[Data.Length * 2];
            Data.CopyTo(narr, 0);
            Data = narr;
        }
    }

    /// <summary>
    /// Returns the first index where the priority is greater than the given value.
    /// </summary>
    protected int NextIndexForPriority(int p) {
        //TODO you can make this binary search or whatever
        for (int ii = 0; ii < count; ++ii) {
            if (Data[ii].Priority > p) return ii;
        }
        return count;
    }

    /// <summary>
    /// Find the first index at which the sorting priority is greater than or equal to the given value.
    /// </summary>
    public int FirstPriorityGT(int priority) => NextIndexForPriority(priority - 1);


    /// <summary>
    /// Add an element to the array with the priority assigned in the DeletionMarker.
    /// </summary>
    public void AddPriority(D dm) {
        MaybeResize();
        Data.Insert(ref count, dm, NextIndexForPriority(dm.Priority));
    }

    /// <summary>
    /// Returns true iff the element at the given index has not been deleted.
    /// </summary>
    public bool ExistsAt(int index) => !Data[index].MarkedForDeletion;
    

    /// <summary>
    /// Get the index'th element's metadata if it has not been deleted.
    /// </summary>
    public bool GetMarkerIfExistsAt(int index, out D val) {
        if (Data[index].MarkedForDeletion) {
            val = default!;
            return false;
        } else {
            val = Data[index];
            return true;
        }
    }
    /// <summary>
    /// Mark the element at the provided index for deletion.
    /// </summary>
    public void Delete(int index) => Data[index].MarkForDeletion();

    
    /// <summary>
    /// Clear the array.
    /// <br/>Note that this will result in markers currently in the array not receiving <see cref="IDeletionMarker.RemovedFromCollection"/>.
    /// </summary>
    public void Empty() {
        Array.Clear(Data, 0, count);
        count = 0;
    }
    
    /// <summary>
    /// Sort the array. 
    /// </summary>
    public void Sort(IComparer<D> cmp) {
        Compact();
        Array.Sort(Data, 0, count, cmp);
    }
    
    
    /// <summary>
    /// Copy the non-deleted elements of this array into a list.
    /// </summary>
    public void CopyIntoList(List<D> dst) {
        for (int ii = 0; ii < count; ++ii)
            if (!Data[ii].MarkedForDeletion)
                dst.Add(Data[ii]);
    }

}
}