using System;
using System.Reactive;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures;

/// <summary>
/// Invoke a callback when two dependencies are fulfilled.
/// </summary>
[PublicAPI]
public class JointCallback<T1,T2,R> {
    /// <summary>
    /// The function that will be called when <see cref="First"/> and <see cref="Second"/> are provided.
    /// </summary>
    public Func<T1,T2,R> Callback { get; }
    
    /// <summary>
    /// First value to be passed to the callback.
    /// </summary>
    public Maybe<T1> First { get; private set; } = Maybe<T1>.None;
    
    /// <summary>
    /// Second value to be passed to the callback.
    /// </summary>
    public Maybe<T2> Second { get; private set; } = Maybe<T2>.None;
    
    public JointCallback(Func<T1,T2,R> callback) {
        Callback = callback;
    }
    
    public JointCallback(Action<T1,T2> callback) {
        if (typeof(R) != typeof(Unit))
            throw new Exception($"Joint callback must have a Unit return type to use Action constructor");
        Callback = (a, b) => {
            callback(a, b);
            return default!;
        };
    }

    /// <summary>
    /// Provide the first value, and invoke the callback if the second value is already provided.
    /// </summary>
    /// <exception cref="Exception">Thrown when the first value is already provided.</exception>
    public Maybe<R> SetFirst(T1 value) {
        if (First.Valid)
            throw new Exception($"First value provided twice to {this}");
        First = value;
        if (Second.Try(out var snd))
            return Callback(value, snd);
        return Maybe<R>.None;
    }
    /// <summary>
    /// Provide the second value, and invoke the callback if the first value is already provided.
    /// </summary>
    /// <exception cref="Exception">Thrown when the second value is already provided.</exception>
    public Maybe<R> SetSecond(T2 value) {
        if (Second.Valid)
            throw new Exception($"Second value provided twice to {this}");
        Second = value;
        if (First.Try(out var fst))
            return Callback(fst, value);
        return Maybe<R>.None;
    }
}