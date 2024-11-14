using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures;

/// <summary>
/// A view over a subsection of a list.
/// </summary>
[PublicAPI]
public class SubList<T>: IReadOnlyList<T> {
    /// <inheritdoc/>
    public int Count { get; }
    /// <summary>
    /// The offset of the start of this view from the start of the original list.
    /// </summary>
    public int Offset { get; }
    /// <summary>
    /// The original list.
    /// </summary>
    public IReadOnlyList<T> Source { get; }

    /// <inheritdoc cref="SubList{T}"/>
    public SubList(IReadOnlyList<T> source, int start, int? len = null) {
        this.Source = source;
        Count = len ?? source.Count - start;
        this.Offset = start;
    }

    /// <inheritdoc/>
    public T this[int index] => Source[Offset + index];
    
    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() {
        for (int ii = 0; ii < Count; ++ii) 
            yield return this[ii];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}