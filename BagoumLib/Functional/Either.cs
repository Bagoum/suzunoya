using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace BagoumLib.Functional {
/// <summary>
/// A value that is either of type L (left) or of type R (right).
/// </summary>
[PublicAPI]
public readonly struct Either<L, R> {
    /// <summary>
    /// True iff the value is of type L.
    /// </summary>
    public bool IsLeft { get; }
    
    /// <summary>
    /// The left-value of this type. Only valid if <see cref="IsLeft"/> is true.
    /// </summary>
    public L Left { get; }
    
    /// <summary>
    /// The right-value of this type. Only valid if <see cref="IsLeft"/> is false.
    /// </summary>
    public R Right { get; }
    
    /// <summary>
    /// !<see cref="IsLeft"/>
    /// </summary>
    [JsonIgnore] public bool IsRight => !IsLeft;
    
    /// <summary>
    /// <see cref="Left"/> if <see cref="IsLeft"/> is true, else a default value.
    /// </summary>
    [JsonIgnore]
    public L? LeftOrNull => IsLeft ? Left : default(L?);

    /// <summary>
    /// <see cref="Left"/> if <see cref="IsLeft"/> is true, else throw.
    /// </summary>
    [JsonIgnore]
    public L LeftOrThrow => IsLeft ? Left : throw new Exception();
    
    /// <summary>
    /// <see cref="Right"/> if <see cref="IsLeft"/> is false, else a default value.
    /// </summary>
    [JsonIgnore]
    public R? RightOrNull => IsLeft ? default(R?) : Right;
    
    /// <summary>
    /// <see cref="Right"/> if <see cref="IsLeft"/> is false, else throw.
    /// </summary>
    [JsonIgnore]
    public R? RightOrThrow => IsLeft ? throw new Exception() : Right;

    /// <summary>
    /// Create a new <see cref="Either{L,R}"/> container.
    /// </summary>
    /// <param name="isLeft">True iff the left value is valid, false iff the right value is valid.</param>
    /// <param name="left">A left value.</param>
    /// <param name="right">A right value.</param>
    public Either(bool isLeft, L left, R right) {
        IsLeft = isLeft;
        Left = left;
        Right = right;
    }
    
    /// <summary>
    /// Create a left-<see cref="Either{L,R}"/>.
    /// </summary>
    /// <param name="left">Left value.</param>
    public Either(L left) {
        IsLeft = true;
        Left = left;
        Right = default!;
    }
    
    /// <summary>
    /// Create a right-<see cref="Either{L,R}"/>.
    /// </summary>
    /// <param name="right">Right value.</param>
    public Either(R right) {
        IsLeft = false;
        Left = default!;
        Right = right;
    }

    /// <summary>
    /// Get the left value if <see cref="IsLeft"/> is true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryL(out L val) {
        val = Left;
        return IsLeft;
    }
    
    /// <summary>
    /// Get the right value if <see cref="IsLeft"/> is false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryR(out R val) {
        val = Right;
        return !IsLeft;
    }
    
    /// <summary>
    /// Get a value T by applying the left function to the left value if <see cref="IsLeft"/> is true,
    /// otherwise applying the right function to the right value.
    /// </summary>
    public T Map<T>(Func<L, T> left, Func<R, T> right) => IsLeft ? left(Left) : right(Right);

    /// <summary>
    /// Functor map for type-constructor Either * R.
    /// </summary>
    public Either<L2, R> FMapL<L2>(Func<L, L2> f) => IsLeft ? 
        new(f(Left)) : 
        new(Right);
    
    /// <summary>
    /// Functor map for type-constructor Either L *.
    /// </summary>
    public Either<L, R2> FMapR<R2>(Func<R, R2> f) => IsLeft ? 
        new(Left) : 
        new(f(Right));
    
    /// <summary>
    /// Monadic bind for type-constructor Either * R.
    /// </summary>
    public Either<L2, R> BindL<L2>(Func<L, Either<L2, R>> f) => IsLeft ? 
        f(Left) : 
        new(Right);
    
    /// <summary>
    /// Monadic bind for type-constructor Either L *.
    /// </summary>
    public Either<L, R2> BindR<R2>(Func<R, Either<L, R2>> f) => IsLeft ? 
        new(Left) : 
        f(Right);
    
    /// <summary>
    /// Applicative apply for type-constructor Either * R.
    /// <br/>x.Apply(f) = apply f x
    /// </summary>
    public Either<L2, R> ApplyL<L2>(in Either<Func<L, L2>, R> f) => f.IsLeft ?
        IsLeft ?
            new(f.Left(Left)) :
            new(Right) :
        new(f.Right);
    
    /// <summary>
    /// Applicative apply for type-constructor Either L *.
    /// <br/>x.Apply(f) = apply f x
    /// </summary>
    public Either<L, R2> ApplyR<R2>(in Either<L, Func<R, R2>> f) => f.IsLeft ?
        new(f.Left) :
        IsLeft ?
            new(Left) :
            new (f.Right(Right));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Either<L, R> other && Equals(other);
    
    /// <summary>
    /// Equality operator. Tests that both objects have the same left/right orientation, and that their valid values are the same.
    /// </summary>
    public bool Equals(Either<L, R> other) => this == other;
    
    /// <inheritdoc/>
    public override int GetHashCode() => IsLeft ? (true, Left).GetHashCode() : (false, Right).GetHashCode();
    
    /// <inheritdoc cref="Equals(Either{L,R})"/>
    public static bool operator ==(in Either<L, R> a, in Either<L, R> b) =>
        (a.IsLeft && b.IsLeft && EqualityComparer<L>.Default.Equals(a.Left, b.Left)) ||
        (!a.IsLeft && !b.IsLeft && EqualityComparer<R>.Default.Equals(a.Right, b.Right));

    /// <summary>
    /// Inverted equality operator.
    /// </summary>
    public static bool operator !=(in Either<L, R> a, in Either<L, R> b) => !(a == b);

    /// <inheritdoc/>
    public override string ToString() => IsLeft ? $"Left<{Left}>" : $"Right<{Right}>";

    /// <inheritdoc cref="Either{L,R}(L)"/>
    public static implicit operator Either<L, R>(L l) => new(l);
    
    /// <inheritdoc cref="Either{L,R}(R)"/>
    public static implicit operator Either<L, R>(R r) => new(r);
}
}