using System;
using System.Collections.Generic;
using System.Linq;

namespace BagoumLib.DataStructures;

/// <summary>
/// An array of data that cannot be modified and has element-based equality/hashing.
/// </summary>
public readonly struct FreezableArray<T> : IEquatable<FreezableArray<T>> {
    /// <summary>
    /// The underlying data.
    /// </summary>
    public IReadOnlyList<T> Data => data;
    private readonly T[] data;
    
    /// <inheritdoc cref="FreezableArray{T}"/>
    public FreezableArray(T[] data) {
        this.data = data;
    }

    /// <summary>
    /// An empty array.
    /// </summary>
    public static FreezableArray<T> Empty { get; } = new([]);

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is FreezableArray<T> td && Data.AreSame(td.Data);
    
    /// <inheritdoc/>
    public bool Equals(FreezableArray<T> other) => Data.AreSame(other.Data);

    /// <inheritdoc/>
    public override int GetHashCode() => Data.ElementWiseHashCode();

    /// <inheritdoc/>
    public override string ToString() => $"Frozen[{string.Join(", ", Data.Select(d => d?.ToString() ?? "<null>"))}]";

}