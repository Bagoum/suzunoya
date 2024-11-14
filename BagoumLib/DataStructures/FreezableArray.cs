using System;
using System.Linq;

namespace BagoumLib.DataStructures;

public readonly struct FreezableArray<T> {
    public readonly T[] Data;
    public FreezableArray(T[] data) {
        this.Data = data;
    }

    //Call this when using as a persistent key so elements don't get modified later
    public FreezableArray<T> Freeze() => 
        new(Data.ToArray());

    public override bool Equals(object obj) =>
        obj is FreezableArray<T> td && Data.AreSame(td.Data);

    public override int GetHashCode() => Data.ElementWiseHashCode();

    public static readonly FreezableArray<T> Empty = new(Array.Empty<T>());

    public override string ToString() => $"Frozen[{string.Join(", ", Data.Select(d => d?.ToString() ?? "<null>"))}]";
}