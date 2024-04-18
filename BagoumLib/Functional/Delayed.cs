using System;
using JetBrains.Annotations;

namespace BagoumLib.Functional;

/// <summary>
/// A value, or a delayed value.
/// </summary>
[PublicAPI]
public readonly struct Delayed<T> {
    /// <summary>
    /// True iff this struct is represented by a concrete value.
    /// </summary>
    public bool IsConcrete { get; }
    
    /// <summary>
    /// A concrete value. Only valid if <see cref="IsConcrete"/> is true.
    /// </summary>
    public T ConcreteValue { get; }
    
    /// <summary>
    /// A delayed value. Only valid if <see cref="IsConcrete"/> is false.
    /// </summary>
    public Func<T> DelayedValue { get; }

    /// <summary>
    /// Retrieves the concrete value (if provided), else realizes the delayed value.
    /// </summary>
    public T Value => IsConcrete ? ConcreteValue : DelayedValue();

    /// <summary>
    /// Create a Delayed struct from a concrete value.
    /// </summary>
    public Delayed(T val) {
        IsConcrete = true;
        ConcreteValue = val;
        DelayedValue = default!;
    }

    /// <summary>
    /// Create a Delayed struct from a delayed value.
    /// </summary>
    public Delayed(Func<T> delayed) {
        IsConcrete = false;
        ConcreteValue = default!;
        DelayedValue = delayed;
    }

    /// <summary>
    /// Functor map over delayed values.
    /// </summary>
    public Delayed<U> FMap<U>(Func<T, U> map) {
        if (IsConcrete)
            return map(Value);
        else {
            var del = DelayedValue;
            return new Delayed<U>(() => map(del()));
        }
    }

    /// <summary>
    /// Create a Delayed struct from a concrete value.
    /// </summary>
    public static implicit operator Delayed<T>(T val) => new(val);

    /// <summary>
    /// Create a Delayed struct from a delayed value.
    /// </summary>
    public static implicit operator Delayed<T>(Func<T> val) => new(val);
    
}