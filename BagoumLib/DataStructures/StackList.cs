using System.Collections;
using System.Collections.Generic;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {

/// <summary>
/// A stack that can also be indexed like a list.
/// </summary>
[PublicAPI]
public class StackList<T> : IReadOnlyList<T> {
    /// <inheritdoc/>
    public int Count { get; private set; }
    private T[] arr;

    /// <inheritdoc cref="StackList{T}"/>
    public StackList(int size = 2) {
        arr = new T[size];
        Count = 0;
    }

    /// <summary>
    /// Add an element to the end of the stack.
    /// </summary>
    public void Push(T obj) {
        while (Count >= arr.Length) {
            var narr = new T[arr.Length * 2];
            arr.CopyTo(narr, 0);
            arr = narr;
        }
        arr[Count++] = obj;
    }

    /// <summary>
    /// Remove and return the last element.
    /// </summary>
    public T Pop() => arr[--Count];
    
    /// <summary>
    /// Return the last element.
    /// </summary>
    public T Peek() => arr[Count - 1];
    
    /// <summary>
    /// Return the `distance`'th element from the back (distance = 1 is the last element).
    /// </summary>
    public T Peek(int distance) => arr[Count - distance];
    
    /// <summary>
    /// Return the last element if the stack is not empty; otherwise, return 
    /// </summary>
    public Maybe<T> MaybePeek() => Count > 0 ? Peek() : Maybe<T>.None;

    /// <summary>
    /// Empty all elements from the stack.
    /// </summary>
    public void Clear() {
        Count = 0;
        for (int ii = 0; ii < arr.Length; ++ii) arr[ii] = default!;
    }

    /// <summary>
    /// Get a reference to the 'ind''th element.
    /// </summary>
    public ref T this[int ind] => ref arr[ind];
    
    /// <inheritdoc/>
    T IReadOnlyList<T>.this[int ind] => arr[ind];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() {
        for (int ii = 0; ii < Count; ++ii) yield return arr[ii];
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
}