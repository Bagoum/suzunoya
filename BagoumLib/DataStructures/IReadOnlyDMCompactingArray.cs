using System.Collections.Generic;

namespace BagoumLib.DataStructures;

/// <summary>
/// Interface for reading from a <see cref="DMCompactingArray{T}"/> or <see cref="AbstractDMCompactingArray{T}"/>.
/// </summary>
public interface IReadOnlyDMCompactingArray<T>: IEnumerable<T> {
    /// <summary>
    /// Number of elements present in the array.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Get the index'th element in the array if it has not been deleted.
    /// </summary>
    public bool GetIfExistsAt(int index, out T val);

    /// <summary>
    /// Returns the number of non-deleted elements in the array.
    /// </summary>
    public int NumberAlive() {
        int total = 0;
        for (int ii = 0; ii < Count; ++ii)
            if (GetIfExistsAt(ii, out _))
                ++total;
        return total;
    }
}