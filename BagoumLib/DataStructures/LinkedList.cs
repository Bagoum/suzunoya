using System.Collections;
using System.Collections.Generic;

namespace BagoumLib.DataStructures {
/// <summary>
/// A data structure containing a value and a nullable pointer to a continuation.
/// </summary>
public record SinglyLinkedList<T>(T Value, SinglyLinkedList<T>? Next = null) : IEnumerable<T> {
    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() {
        yield return Value;
        if (Next != null)
            foreach (var v in Next)
                yield return v;
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
}