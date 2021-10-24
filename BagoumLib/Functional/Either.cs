﻿using System;
using System.Collections.Generic;
using BagoumLib.Reflection;
using JetBrains.Annotations;

namespace BagoumLib.Functional {
[PublicAPI]
public readonly struct Either<L, R> {

    public bool IsLeft { get; }
    public L Left { get; }
    public R Right { get; }

    public Either(L left) {
        IsLeft = true;
        Left = left;
        Right = default!;
    }
    public Either(R right) {
        IsLeft = false;
        Left = default!;
        Right = right;
    }

    public T Map<T>(Func<L, T> left, Func<R, T> right) => IsLeft ? left(Left) : right(Right);

    public Either<L2, R> FMapL<L2>(Func<L, L2> f) => IsLeft ? 
        new(f(Left)) : 
        new(Right);
    public Either<L, R2> FMapR<R2>(Func<R, R2> f) => IsLeft ? 
        new(Left) : 
        new(f(Right));
    
    public Either<L2, R> BindL<L2>(Func<L, Either<L2, R>> f) => IsLeft ? 
        f(Left) : 
        new(Right);
    public Either<L, R2> BindR<R2>(Func<R, Either<L, R2>> f) => IsLeft ? 
        new(Left) : 
        f(Right);

    public Either<L2, R> ApplyL<L2>(Either<Func<L, L2>, R> f) => f.IsLeft ?
        IsLeft ?
            new(f.Left(Left)) :
            new(Right) :
        new(f.Right);
    public Either<L, R2> ApplyR<R2>(Either<L, Func<R, R2>> f) => f.IsLeft ?
        new(f.Left) :
        IsLeft ?
            new(Left) :
            new (f.Right(Right));



    public override bool Equals(object? obj) => obj is Either<L, R> other && Equals(other);
    public bool Equals(Either<L, R> other) => this == other;

    public override int GetHashCode() => IsLeft ? (true, Left).GetHashCode() : (false, Right).GetHashCode();
    
    public static bool operator ==(Either<L, R> a, Either<L, R> b) =>
        (a.IsLeft && b.IsLeft && Equals(a.Left, b.Left)) ||
        (!a.IsLeft && !b.IsLeft && Equals(a.Right, b.Right));

    public static bool operator !=(Either<L, R> a, Either<L, R> b) => !(a == b);

    public override string ToString() => IsLeft ? $"Left<{Left}>" : $"Right<{Right}>";

}
}