using System;
using System.Collections.Generic;
using BagoumLib.Reflection;
using JetBrains.Annotations;

namespace BagoumLib.Functional {
[PublicAPI]
public readonly struct Maybe<T> {
    public bool Valid { get; }
    public T Value { get; }

    public (bool, T) Tuple => (Valid, Valid ? Value : default!);
    public Maybe(bool valid, T val) {
        this.Valid = valid;
        this.Value = val;
    }

    public static Maybe<T> Of(T val) => new Maybe<T>(true, val);
    public static readonly Maybe<T> None = new Maybe<T>(false, default!);

    public Maybe<U> FMap<U>(Func<T, U> f) => Valid ? Maybe<U>.Of(f(Value)) : Maybe<U>.None;
    public T Or(T dflt) => Valid ? Value : dflt;

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
}
}