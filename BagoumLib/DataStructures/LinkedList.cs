using System.Collections;
using System.Collections.Generic;

namespace BagoumLib.DataStructures {
public record SinglyLinkedList<T>(T Value, SinglyLinkedList<T>? Next = null) : IEnumerable<T> {
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