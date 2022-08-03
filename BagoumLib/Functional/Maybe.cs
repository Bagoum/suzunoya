using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BagoumLib.Reflection;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace BagoumLib.Functional {
[PublicAPI]
public readonly struct Maybe<T> {
    public bool Valid { get; }
    public T Value { get; }

    [JsonIgnore]
    public (bool, T) Tuple => (Valid, Valid ? Value : default!);
    [JsonIgnore]
    public T? ValueOrNull => Valid ? Value : default(T?);
    
    [JsonConstructor]
    public Maybe(bool valid, T value) {
        this.Valid = valid;
        this.Value = value;
    }
    public Maybe(T val) {
        this.Valid = true;
        this.Value = val;
    }

    public static Maybe<T> Of(T val) => new (val);
    public static readonly Maybe<T> None = new (false, default!);

    public Maybe<U> FMap<U>(Func<T, U> f) => Valid ? new(f(Value)) : Maybe<U>.None;
    public T Or(T dflt) => Valid ? Value : dflt;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Try(out T val) {
        val = Value;
        return Valid;
    }

    
    public bool Equals(Maybe<T> other) => Equals(Tuple, other.Tuple);

    public override bool Equals(object? obj) => obj is Maybe<T> other && Equals(other);

    public override int GetHashCode() => Tuple.GetHashCode();
    
    public static bool operator ==(Maybe<T> a, Maybe<T> b) =>
        (a.Valid == b.Valid) && (!a.Valid || Equals(a.Value, b.Value));

    public static bool operator !=(Maybe<T> a, Maybe<T> b) => !(a == b);

    public override string ToString() => Valid ? $"Some({Value?.ToString() ?? "null"})" : $"None<{typeof(T).RName()}>";

    public static implicit operator Maybe<T>(T val) => Maybe<T>.Of(val);
}
}