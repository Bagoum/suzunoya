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
    protected int count;
    public int Count => count;
    protected bool[] rem;
    //Leaving this public for low-level efficiency
    public T[] Data { get; private set; }
    public int NullElements { get; protected set; } = 0;
    private readonly int firstResize;

    public CompactingArray(int size = 8, int firstResize=16) {
        Data = new T[size];
        rem = new bool[size];
        count = 0;
        this.firstResize = firstResize;
    }

    public void Delete(int ind) {
        rem[ind] = true;
        ++NullElements;
    }

    public void Compact() {
        if (NullElements > 0) {
            int ii = 0;

            while (true) {
                if (ii == count)
                    return;
                if (rem[ii++]) {
                    rem[ii - 1] = false;
                    break;
                }
            }
            int deficit = 1;
            int start_copy = ii;
            for (; ii < count; ++ii) {
                if (rem[ii]) {
                    //Found an empty space
                    if (ii > start_copy) 
                        //There is at least one element to push backwards
                        Array.Copy(Data, start_copy, Data, start_copy - deficit, ii - start_copy);
                    
                    rem[ii] = false;
                    ++deficit;
                    start_copy = ii + 1;
                }
            }
            if (count > start_copy)
                Array.Copy(Data, start_copy, Data, start_copy - deficit, count - start_copy);
            count -= deficit;
            NullElements = 0;
        }
    }

    public void AddRef(ref T obj) {
        if (count >= Data.Length) {
            var nLen = Math.Max(Data.Length * 2, firstResize);
            var narr = new T[nLen];
            Data.CopyTo(narr, 0);
            Data = narr;
            var nrem = new bool[nLen];
            rem.CopyTo(nrem, 0);
            rem = nrem;
        }
        rem[count] = false;
        Data[count++] = obj;
    }

    public void Add(T obj) => AddRef(ref obj);

    public void Empty() {
        Array.Clear(Data, 0, Data.Length);
        Array.Clear(rem, 0, rem.Length);
        count = 0;
    }

    public ref T this[int index] => ref Data[index];
    public T ItemAt(int index) => Data[index];

    public bool TryGet(int index, out T obj) {
        if (rem[index]) {
            obj = default!;
            return false;
        } else {
            obj = Data[index];
            return true;
        }
    }
}
}