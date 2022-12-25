using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BagoumLib.Reflection;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace BagoumLib.Functional {
/// <summary>
/// A value of type T that may or may not exist.
/// </summary>
[PublicAPI]
public readonly struct Maybe<T> {
    /// <summary>
    /// True iff the value exists.
    /// </summary>
    public bool Valid { get; }
    
    /// <summary>
    /// The underlying value. Undefined if <see cref="Valid"/> is false.
    /// </summary>
    public T Value { get; }

    [JsonIgnore]
    private (bool, T) Tuple => (Valid, Valid ? Value : default!);
    
    /// <summary>
    /// Get the value if it is present, otherwise get default(T).
    /// </summary>
    [JsonIgnore]
    public T? ValueOrNull => Valid ? Value : default(T?);
    
    /// <summary>
    /// Create a <see cref="Maybe{T}"/>.
    /// </summary>
    /// <param name="valid">True iff the value is present.</param>
    /// <param name="value">The underlying value.</param>
    [JsonConstructor]
    public Maybe(bool valid, T value) {
        this.Valid = valid;
        this.Value = value;
    }
    
    /// <summary>
    /// Create a <see cref="Maybe{T}"/> with a value that exists.
    /// </summary>
    public Maybe(T val) {
        this.Valid = true;
        this.Value = val;
    }

    /// <summary>
    /// Create a <see cref="Maybe{T}"/> with a value that exists.
    /// </summary>
    public static Maybe<T> Of(T val) => new (val);
    
    /// <summary>
    /// Create a <see cref="Maybe{T}"/> representing no existing value.
    /// </summary>
    public static readonly Maybe<T> None = new (false, default!);

    /// <summary>
    /// Functor map.
    /// </summary>
    public Maybe<U> FMap<U>(Func<T, U> f) => Valid ? new(f(Value)) : Maybe<U>.None;
    
    /// <summary>
    /// Get the underlying value if it exists, otherwise return dflt.
    /// </summary>
    public T Or(T dflt) => Valid ? Value : dflt;

    /// <summary>
    /// Get the underlying value if it exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Try(out T val) {
        val = Value;
        return Valid;
    }

    /// <summary>
    /// Equality operator. Checks that either both maybes are valid and contain the same underlying value,
    /// or that they are both invalid.
    /// </summary>
    public bool Equals(Maybe<T> other) => this == other;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Maybe<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Tuple.GetHashCode();
    
    /// <inheritdoc cref="Equals(BagoumLib.Functional.Maybe{T})"/>
    public static bool operator ==(Maybe<T> a, Maybe<T> b) =>
        (a.Valid == b.Valid) && (!a.Valid || Equals(a.Value, b.Value));

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Maybe<T> a, Maybe<T> b) => !(a == b);

    /// <inheritdoc/>
    public override string ToString() => Valid ? $"Some({Value?.ToString() ?? "null"})" : $"None<{typeof(T).RName()}>";

    /// <inheritdoc cref="Maybe{T}.Of"/>
    public static implicit operator Maybe<T>(T val) => Maybe<T>.Of(val);
}
}