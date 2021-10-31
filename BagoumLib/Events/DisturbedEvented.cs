using System;
using System.Reactive.Subjects;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// DisturbedEvented is a wrapper around a core value (eg. a location) that can be modified by any number
///  of disturbance effects (eg. shake effects) that all use the same aggregation function (eg. addition).
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
            onSet.OnNext(ComputeValue());
        }
    }
    private T ComputeValue() {
        var agg = _baseValue;
        disturbances.Compact();
        for (int ii = 0; ii < disturbances.Count; ++ii)
            if (disturbances[ii].LastPublished.Try(out var right))
                agg = Fold(agg, right);
        return agg;
    }
    public T Value {
        get => onSet.Value;
        set => BaseValue = value;
    }
    public Maybe<T> LastPublished => onSet.LastPublished;

    private readonly Evented<T> onSet;
    private readonly DMCompactingArray<IBObservable<T>> disturbances = new();

    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    public DisturbedEvented(T val) {
        _baseValue = val;
        onSet = new Evented<T>(ComputeValue());
    }

    public static implicit operator T(DisturbedEvented<T> evo) => evo.Value;

    protected abstract T Fold(T left, T right);
    
    /// <summary>
    /// Add a disturbance.
    /// Whenver the disturbance receives a new value, this aggregate will update as well.
    /// </summary>
    public IDisposable AddDisturbance(IBObservable<T> disturbance) {
        var trackToken = disturbances.Add(disturbance);
        var updateToken = disturbance.Subscribe(_ => DoPublishIfNotSame());
        return new JointDisposable(DoPublishIfNotSame, trackToken, updateToken);
    }

    public IDisposable AddConst(T value) {
        var trackToken = disturbances.Add(new ConstantObservable<T>(value));
        DoPublishIfNotSame();
        return new JointDisposable(DoPublishIfNotSame, trackToken);
    }

    private void DoPublishIfNotSame() {
        var nxt = ComputeValue();
        if (Equals(Value, nxt))
            return;
        onSet.OnNext(nxt);
    }

    public void ClearDisturbances() {
        disturbances.Empty();
        DoPublishIfNotSame();
    }

    public IDisposable Subscribe(IObserver<T> observer) => onSet.Subscribe(observer);

    public void OnNext(T value) => BaseValue = value;

    public void OnError(Exception error) => onSet.OnError(error);

    public void OnCompleted() => onSet.OnCompleted();
}

public class DisturbedFold<T> : DisturbedEvented<T> {
    private readonly Func<T, T, T> folder;
    public DisturbedFold(T val, Func<T, T, T> folder) : base(val) {
        this.folder = folder;
    }
    protected override T Fold(T left, T right) => folder(left, right);
}

/// <summary>
/// Get the most recent value.
/// </summary>
public class DisturbedOverride<T> : DisturbedEvented<T> {
    public DisturbedOverride(T val) : base(val) { }
    protected override T Fold(T left, T right) => right;
}

/// <summary>
/// Get the sum of all values.
/// </summary>
public class DisturbedSum<T> : DisturbedEvented<T> {
    private readonly Func<T, T, T> adder = GenericOps.GetAddOp<T>();
    public DisturbedSum(T val) : base(val) { }
    protected override T Fold(T left, T right) => adder(left, right);
}
/// <summary>
/// Get the product of all values.
/// </summary>
public class DisturbedProduct<T> : DisturbedEvented<T> {
    private readonly Func<T, T, T> prod = GenericOps.GetVecMulOp<T>();
    public DisturbedProduct(T val) : base(val) { }
    protected override T Fold(T left, T right) => prod(left, right);
}
public class DisturbedAnd : DisturbedEvented<bool> {
    public DisturbedAnd() : base(true) { }
    protected override bool Fold(bool left, bool right) => left && right;
}
public class DisturbedOr : DisturbedEvented<bool> {
    public DisturbedOr() : base(false) { }
    protected override bool Fold(bool left, bool right) => left || right;
}


}