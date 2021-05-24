using System;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// An ordered collection that supports iteration, as well as deletion of arbitrary indices.
/// Indices are not guaranteed to be persistent, so deletion must occur during the iteration block of an index.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class CompactingArray<T> {
    protected int count;
    public int Count => count;
    protected bool[] rem;
    protected T[] arr;
    public int NullElements { get; protected set; } = 0;
    private readonly int firstResize;

    public CompactingArray(int size = 8, int firstResize=16) {
        arr = new T[size];
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
                        Array.Copy(arr, start_copy, arr, start_copy - deficit, ii - start_copy);
                    
                    rem[ii] = false;
                    ++deficit;
                    start_copy = ii + 1;
                }
            }
            if (count > start_copy)
                Array.Copy(arr, start_copy, arr, start_copy - deficit, count - start_copy);
            count -= deficit;
            NullElements = 0;
        }
    }

    public void Add(ref T obj) {
        if (count >= arr.Length) {
            var nLen = Math.Max(arr.Length * 2, firstResize);
            var narr = new T[nLen];
            arr.CopyTo(narr, 0);
            arr = narr;
            var nrem = new bool[nLen];
            rem.CopyTo(nrem, 0);
            rem = nrem;
        }
        rem[count] = false;
        arr[count++] = obj;
    }

    public void AddV(T obj) => Add(ref obj);

    public void Empty() {
        Array.Clear(arr, 0, arr.Length);
        Array.Clear(rem, 0, rem.Length);
        count = 0;
    }

    public ref T this[int index] => ref arr[index];

    public bool TryGet(int index, out T obj) {
        if (rem[index]) {
            obj = default!;
            return false;
        } else {
            obj = arr[index];
            return true;
        }
    }
}
}