using System;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// Fast resizeable wrapper around T[] that is safe for arbitrary indexing.
/// </summary>
[PublicAPI]
public class SafeResizableArray<T> {
    private int baseSize;
    private int count;
    private T[] arr;

    public SafeResizableArray(int size = 8) {
        arr = new T[baseSize = size];
        count = 0;
    }

    public void SafeAssign(int index, T value) {
        while (index >= arr.Length) {
            var narr = new T[arr.Length * 2];
            arr.CopyTo(narr, 0);
            arr = narr;
        }
        arr[index++] = value;
        if (index > count) count = index;
    }

    public T SafeGet(int index) {
        while (index >= arr.Length) {
            var narr = new T[arr.Length * 2];
            arr.CopyTo(narr, 0);
            arr = narr;
        }
        return arr[index];
    }

    /// <summary>
    /// Clear all elements in the array and reset it to its base size.
    /// </summary>
    public void EmptyAndReset() {
        Array.Clear(arr, 0, arr.Length);
        count = 0;
    }

}
}