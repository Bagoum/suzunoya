using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {


[PublicAPI]
public class StackList<T> : IEnumerable<T> {
    public int Count { get; private set; }
    private T[] arr;

    public StackList(int size = 2) {
        arr = new T[size];
        Count = 0;
    }

    public void Push(T obj) {
        while (Count >= arr.Length) {
            var narr = new T[arr.Length * 2];
            arr.CopyTo(narr, 0);
            arr = narr;
        }
        arr[Count++] = obj;
    }

    public T Pop() => arr[--Count];
    public T Peek() => arr[Count - 1];

    public void Clear() {
        Count = 0;
        for (int ii = 0; ii < arr.Length; ++ii) arr[ii] = default!;
    }

    public ref T this[int ind] => ref arr[ind];

    public IEnumerator<T> GetEnumerator() {
        for (int ii = 0; ii < Count; ++ii) yield return arr[ii];
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
}