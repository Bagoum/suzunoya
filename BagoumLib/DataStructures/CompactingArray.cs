using System;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// An ordered collection that supports iteration, as well as deletion of arbitrary indices.
/// Indices are not guaranteed to be persistent, so deletion must occur during the iteration block of an index.
/// <br/>Note: this is significantly less flexible than <see cref="DMCompactingArray{T}"/>, but
/// can function with zero reference overhead and zero garbage.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class CompactingArray<T> {
    /// <summary>
    /// Number of elements present in the array.
    /// Some of the elements may be deleted. Call <see cref="Compact"/> to remove deleted elements and make this number
    ///  stricter.
    /// </summary>
    protected int count;
    /// <inheritdoc cref="count"/>
    public int Count => count;
    /// <summary>
    /// Iff the object at index i has been marked for deletion, then the i'th element in this array is true.
    /// </summary>
    public bool[] Deleted { get; private set; }
    //Leaving this public for low-level efficiency
    /// <summary>
    /// Underlying data array.
    /// </summary>
    public T[] Data { get; private set; }
    /// <summary>
    /// Number of elements that are marked for deletion.
    /// </summary>
    public int NullElements { get; protected set; } = 0;
    private readonly int firstResize;

    public CompactingArray(int size = 8, int firstResize=16) {
        Data = new T[size];
        Deleted = new bool[size];
        count = 0;
        this.firstResize = firstResize;
    }

    /// <inheritdoc cref="AnyTypeDMCompactingArray{D}.Delete"/>
    public void Delete(int ind) {
        Deleted[ind] = true;
        ++NullElements;
    }

    /// <inheritdoc cref="AnyTypeDMCompactingArray{D}.Compact"/>
    public bool Compact() {
        if (NullElements <= 0) return false; 
        int ii = 0;
        while (true) {
            if (ii == count) {
                NullElements = 0;
                return false;
            }
            if (Deleted[ii++]) {
                Deleted[ii - 1] = false;
                break;
            }
        }
        int deficit = 1;
        int start_copy = ii;
        for (; ii < count; ++ii) {
            if (Deleted[ii]) {
                //Found an empty space
                if (ii > start_copy) 
                    //There is at least one element to push backwards
                    Array.Copy(Data, start_copy, Data, start_copy - deficit, ii - start_copy);
                
                Deleted[ii] = false;
                ++deficit;
                start_copy = ii + 1;
            }
        }
        if (count > start_copy)
            Array.Copy(Data, start_copy, Data, start_copy - deficit, count - start_copy);
        count -= deficit;
        NullElements = 0;
        return true;
    }

    public void AddRef(ref T obj) {
        if (count >= Data.Length) {
            var nLen = Math.Max(Data.Length * 2, firstResize);
            var narr = new T[nLen];
            Data.CopyTo(narr, 0);
            Data = narr;
            var nrem = new bool[nLen];
            Deleted.CopyTo(nrem, 0);
            Deleted = nrem;
        }
        Deleted[count] = false;
        Data[count++] = obj;
    }

    public void Add(T obj) => AddRef(ref obj);

    /// <inheritdoc cref="AnyTypeDMCompactingArray{D}.Empty"/>
    public void Empty() {
        Array.Clear(Data, 0, Data.Length);
        Array.Clear(Deleted, 0, Deleted.Length);
        count = 0;
        NullElements = 0;
    }

    public ref T this[int index] => ref Data[index];
    public T ItemAt(int index) => Data[index];

    public bool TryGet(int index, out T obj) {
        if (Deleted[index]) {
            obj = default!;
            return false;
        } else {
            obj = Data[index];
            return true;
        }
    }
}
}