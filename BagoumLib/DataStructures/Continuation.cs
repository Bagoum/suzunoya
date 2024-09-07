using System;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures;

/// <summary>
/// An object of some type paired with a function that converts it to a known type.
/// </summary>
/// <typeparam name="B">A base type for the underlying object.</typeparam>
/// <typeparam name="U">The known type that is the result of the continuation.</typeparam>
[PublicAPI]
public abstract record Continuation<B, U> {
    /// <summary>
    /// Run the continuation on the underlying object.
    /// </summary>
    public abstract U Realize();

    /// <summary>
    /// Get the underlying value in a base type.
    /// </summary>
    public abstract B Obj { get; }
    
    private record Typed<T>(T TObj, Func<T, U> Cont) : Continuation<B, U> where T: B {
        /// <inheritdoc/>
        public override U Realize() => Cont(TObj);

        public override B Obj => TObj;
    }

    /// <summary>
    /// Create a <see cref="Continuation{B,U}"/> for some data.
    /// </summary>
    public static Continuation<B, U> Of<T>(T data, Func<T, U> cont) where T: B
        => new Typed<T>(data, cont);
}
