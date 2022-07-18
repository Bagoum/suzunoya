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
/// <br/>Note that <see cref="get_Value"/> and the event listeners
///  will return the total value, whereas <see cref="set_Value"/> will set the core value.
/// <br/>Use <see cref="get_BaseValue"/> to get the core value. <see cref="set_BaseValue"/>
///  will also set the core value.
/// <br/>Note that, like <see cref="Evented{T}"/>, new subscribers will be sent the current value immediately.
/// </summary>
[PublicAPI]
public abstract class DisturbedEvented<T> : ICSubject<T> {
    private T _baseValue;
    public T BaseValue {
        get => _baseValue;
        set {
            this._baseValue = value;
            DoPublishIfNotSame();
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

    /// <summary>
    /// Copy the disturbances from another <see cref="DisturbedEvented{T}"/>.
    /// The copied disturbances will be bound by the same <see cref="IDisposable"/>s as the original.
    /// </summary>
    /// <param name="src"></param>
    public void CopyDisturbances(DisturbedEvented<T> src) {
        for (int ii = 0; ii < src.disturbances.Count; ++ii)
            if (src.disturbances.GetMarkerIfExistsAt(ii, out var m))
                disturbances.AddPriority(m);
        DoPublishIfNotSame();
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
public class OverrideEvented<T> : DisturbedEvented<T> {
    public OverrideEvented(T val) : base(val) { }
    protected override T Fold(T left, T right) => right;
}

/// <summary>
/// Get the sum of all values.
/// </summary>
public class DisturbedSum<T> : DisturbedEvented<T> {
    private static readonly (T zero, Func<T, T, T> add) monoid = GenericOps.GetAddOp<T>();
    public DisturbedSum() : base(monoid.zero) { }
    public DisturbedSum(T val) : base(val) { }

    public DisturbedSum(IBObservable<T> val) : base(monoid.zero) {
        AddDisturbance(val);
    }
    protected override T Fold(T left, T right) => monoid.add(left, right);
}
/// <summary>
/// Get the product of all values.
/// </summary>
public class DisturbedProduct<T> : DisturbedEvented<T> {
    private static readonly (T zero, Func<T, T, T> add) monoid = GenericOps.GetVecMulOp<T>();
    public DisturbedProduct() : base(monoid.zero) { }
    public DisturbedProduct(T val) : base(val) { }
    
    public DisturbedProduct(IBObservable<T> val) : base(monoid.zero) {
        AddDisturbance(val);
    }
    protected override T Fold(T left, T right) => monoid.add(left, right);
}
public class DisturbedAnd : DisturbedEvented<bool> {
    public DisturbedAnd(bool first=true) : base(first) { }
    protected override bool Fold(bool left, bool right) => left && right;
}
public class DisturbedOr : DisturbedEvented<bool> {
    public DisturbedOr(bool first=false) : base(first) { }
    protected override bool Fold(bool left, bool right) => left || right;
}


}