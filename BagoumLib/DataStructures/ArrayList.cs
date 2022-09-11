using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// Implementation of List that supports Add(in T). This is probably not faster than List.
/// </summary>
[PublicAPI]
public class ArrayList<T> : ICollection<T> {
    private int count;
    private T[] arr;

    public ArrayList(int size = 8) {
        arr = new T[size];
        count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity() {
        if (count == arr.Length) {
            var narr = new T[arr.Length * 2];
            Array.Copy(arr, 0, narr, 0, count);
            arr = narr;
        }
    }

    public IEnumerator<T> GetEnumerator() {
        for (int ii = 0; ii < count; ++ii)
            yield return arr[ii];
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public void AddIn(in T item) {
        if (count == arr.Length)
            EnsureCapacity();
        arr[count++] = item;
    }
    public void Add(T item) {
        if (count == arr.Length)
            EnsureCapacity();
        arr[count++] = item;
    }

    public void Clear() {
        Array.Clear(arr, 0, arr.Length);
        count = 0;
    }

    public bool Contains(T item) {
        EqualityComparer<T> equalityComparer = EqualityComparer<T>.Default;
        for (int ii = 0; ii < count; ++ii)
            if (equalityComparer.Equals(arr[ii], item))
                return true;
        return false;
    }

    public void CopyTo(T[] array, int arrayIndex) {
        Array.Copy(arr, 0, array, arrayIndex, count);
    }

    public bool Remove(T item) {
        int index = this.IndexOf(item);
        if (index < 0)
            return false;
        this.RemoveAt(index);
        return true;
    }

    public int Count => count;
    public bool IsReadOnly => false;
    public int IndexOf(T item) => Array.IndexOf(arr, item, 0, count);

    public void Insert(int index, T item) {
        Array.Copy(arr, index, arr, index + 1, count++ - index);
        arr[index] = item;
    }

    public void RemoveAt(int index) {
        Array.Copy(arr, index + 1, arr, index, --count - index);
        arr[count] = default!;
    }

    public ref T this[int index] => ref arr[index];
}
}