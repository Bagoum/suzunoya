using System;
using System.Reactive.Subjects;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// DisturbedEvented is a wrapper around a core value (eg. a location) that can be modified by any number
///  of disturbance effects (eg. shake effects).
/// Note that get_Value, LastPublished, and the onChange event will return the total value,
///  whereas set_Value will set the core value.
/// Use get_BaseValue to get the core value. set_BaseValue will also set the core value.
/// </summary>
[PublicAPI]
public abstract class DisturbedEvented<T> : IBSubject<T> {
    private T _baseValue;
    
    public T BaseValue {
        get => _baseValue;
        set {
            this._baseValue = value;
            onSet.OnNext(Value);
        }
    }
    public T Value {
        get {
            var agg = _baseValue;
            for (int ii = 0; ii < disturbances.Count; ++ii) {
                if (disturbances.ExistsAt(ii) && disturbances[ii].LastPublished.Try(out var right))
                    agg = Fold(agg, right);
            }
            return agg;
        }
        set => BaseValue = value;
    }
    public Maybe<T> LastPublished => onSet.LastPublished;

    private readonly Event<T> onSet;
    private readonly DMCompactingArray<IBObservable<T>> disturbances = new();

    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    public DisturbedEvented(T val) {
        _baseValue = val;
        onSet = new Event<T>();
        onSet.OnNext(Value);
    }

    public static implicit operator T(DisturbedEvented<T> evo) => evo.Value;

    protected abstract T Fold(T left, T right);


    public IDisposable AddDisturbance(IBObservable<T> disturbance) {
        var trackToken = disturbances.Add(disturbance);
        var updateToken = disturbance.Subscribe(_ => DoPublishIfNotSame());
        return new JointDisposable(DoPublishIfNotSame, trackToken, updateToken);
    }
    
    private void DoPublishIfNotSame() {
        var nxt = Value;
        if (LastPublished.Try(out var last) && Equals(nxt, last))
            return;
        onSet.OnNext(nxt);
    }

    public IDisposable Subscribe(IObserver<T> observer) {
        observer.OnNext(Value);
        return onSet.Subscribe(observer);
    }

    public void OnNext(T value) => Value = value;

    public void OnError(Exception error) => onSet.OnError(error);

    public void OnCompleted() => onSet.OnCompleted();
}

public class DisturbedSum<T> : DisturbedEvented<T> {
    private readonly Func<T, T, T> adder = GenericOps.GetAddOp<T>();
    public DisturbedSum(T val) : base(val) { }
    protected override T Fold(T left, T right) => adder(left, right);
}
public class DisturbedProduct<T> : DisturbedEvented<T> {
    private readonly Func<T, T, T> prod = GenericOps.GetVecMulOp<T>();
    public DisturbedProduct(T val) : base(val) { }
    protected override T Fold(T left, T right) => prod(left, right);
}
public class DisturbedAnd : DisturbedEvented<bool> {
    public DisturbedAnd(bool val) : base(val) { }
    protected override bool Fold(bool left, bool right) => left && right;
}
public class DisturbedOr : DisturbedEvented<bool> {
    public DisturbedOr(bool val) : base(val) { }
    protected override bool Fold(bool left, bool right) => left || right;
}

}