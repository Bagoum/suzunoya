using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
[PublicAPI]
public class CircularList<T> : IEnumerable<T> {
    public int Count { get; private set; }
    private readonly T[] arr;
    private int pointer;

    public CircularList(int size) {
        arr = new T[size];
        pointer = 0;
        Count = 0;
    }

    public void Add(T obj) {
        arr[pointer] = obj;
        pointer = (pointer + 1) % arr.Length;
        Count = Math.Min(Count + 1, arr.Length);
    }

    public T SafeIndexFromBack(int ii) {
        ii = BMath.Clamp(1, Count, ii);
        return arr[BMath.Mod(arr.Length, pointer - ii)];
    }

    public void Clear() {
        Count = 0;
        pointer = 0;
        for (int ii = 0; ii < arr.Length; ++ii) arr[ii] = default!;
    }
    
    public ref T this[int index] => ref arr[BMath.Mod(arr.Length, pointer - Count + index)];

    public IEnumerator<T> GetEnumerator() {
        for (int ii = 0; ii < Count; ++ii) {
            yield return this[ii];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
}