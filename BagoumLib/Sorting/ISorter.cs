using System.Collections.Generic;

namespace BagoumLib.Sorting {

/// <summary>
/// Returns true iff a is less than or equal to b.
/// </summary>
public delegate bool LeqCompare<T>(in T a, in T b);
public interface ISorter<T> {
    void Sort(T[] array, int start, int end, LeqCompare<T> comp);
}
}