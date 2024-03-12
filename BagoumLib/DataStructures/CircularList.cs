using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// A list with a fixed capacity that wraps around and overwrites the oldest items.
/// </summary>
[PublicAPI]
public class CircularList<T> : IEnumerable<T>, IReadOnlyList<T> {
    /// <summary>
    /// Number of elements in the list. Note that this is bounded by the initialized size.
    /// </summary>
    public int Count { get; private set; }
    
    /// <summary>
    /// The total number of items that were added to the list. Since old items are overwritten,
    ///  only the items in the range [TotalAdds-Count,TotalAdds) are accessible.
    /// </summary>
    public int TotalAdds { get; private set; }
    private readonly T[] arr;
    private int pointer;

    /// <summary>
    /// Create a circular list.
    /// </summary>
    /// <param name="size">Fixed capacity of the circular list.</param>
    public CircularList(int size) {
        arr = new T[size];
        pointer = 0;
        Count = 0;
    }

    /// <summary>
    /// Add an element. Overwrite old elements if the container is full.
    /// </summary>
    public void Add(T obj) {
        arr[pointer] = obj;
        pointer = (pointer + 1) % arr.Length;
        Count = Math.Min(Count + 1, arr.Length);
        ++TotalAdds;
    }

    /// <summary>
    /// Get the ii'th most recently added element.
    /// </summary>
    /// <param name="ii">Index from end of container. Note this should be 1 to get the last element in the container.</param>
    /// <returns></returns>
    public T SafeIndexFromBack(int ii) {
        ii = BMath.Clamp(1, Count, ii);
        return arr[BMath.Mod(arr.Length, pointer - ii)];
    }

    /// <summary>
    /// Get the ii'th added element. If the ii'th added element has not been overwritten,
    ///  then this is equivalent to indexing into an unbounded array holding the added values at index ii.
    /// </summary>
    public ref T TrueIndex(int ii) => ref arr[BMath.Mod(arr.Length, ii)];
    
    /// <inheritdoc/>
    T IReadOnlyList<T>.this[int index] => TrueIndex(index);
    
    /// <summary>
    /// Retrieve the (size-index)'th most recently added element.
    /// <br/>Eg. ii=0 is the oldest element still in the list; ii=Count-1 is the newest element.
    /// </summary>
    public ref T RelativeIndex(int ii) => ref arr[BMath.Mod(arr.Length, pointer - Count + ii)];

    /// <summary>
    /// Clear all emenets in the container.
    /// </summary>
    public void Clear() {
        Count = 0;
        TotalAdds = 0;
        pointer = 0;
        Array.Clear(arr, 0, arr.Length);
    }
    

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() {
        for (int ii = 0; ii < Count; ++ii) {
            yield return this.RelativeIndex(ii);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
}