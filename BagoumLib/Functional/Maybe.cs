using System;
using JetBrains.Annotations;

namespace BagoumLib.Functional {
[PublicAPI]
public readonly struct Maybe<T> {
    public bool Valid { get; }
    public T Value { get; }
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

}
}