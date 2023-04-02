using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Initial (aggregation seed) value.
    /// </summary>
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
    
    /// <summary>
    /// Aggregate value. Note that the set operator modifies <see cref="BaseValue"/>.
    /// </summary>
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

    /// <summary>
    /// Get the aggregate value.
    /// </summary>
    public static implicit operator T(DisturbedEvented<T> evo) => evo.Value;

    /// <summary>
    /// Function that aggregates values.
    /// </summary>
    /// <param name="acc">Accumulated value</param>
    /// <param name="next">Next value to fold into acc</param>
    /// <returns></returns>
    protected abstract T Fold(T acc, T next);
    
    /// <summary>
    /// Add a disturbance.
    /// Whenver the disturbance receives a new value, this aggregate will update as well.
    /// </summary>
    public IDisposable AddDisturbance(IBObservable<T> disturbance) {
        var trackToken = disturbances.Add(disturbance);
        var updateToken = disturbance.Subscribe(_ => DoPublishIfNotSame());
        return new JointDisposable(DoPublishIfNotSame, trackToken, updateToken);
    }

    /// <summary>
    /// Add a constant value disturbance.
    /// This aggregate will also update immediately.
    /// </summary>
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
            if (src.disturbances.GetMarkerIfExistsAt(ii, out var m)) {
                m.DisallowPooling();
                disturbances.AddPriority(m);
            }
        DoPublishIfNotSame();
    }

    private void DoPublishIfNotSame() {
        var nxt = ComputeValue();
        if (EqualityComparer<T>.Default.Equals(nxt, Value))
            return;
        onSet.OnNext(nxt);
    }

    /// <summary>
    /// Remove all additional provided values. 
    /// </summary>
    public void ClearDisturbances() {
        disturbances.Empty();
        DoPublishIfNotSame();
    }
    
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer) => onSet.Subscribe(observer);

    /// <inheritdoc/>
    public void OnNext(T value) => BaseValue = value;

    /// <inheritdoc/>
    public void OnError(Exception error) => onSet.OnError(error);

    /// <inheritdoc/>
    public void OnCompleted() {
        onSet.OnCompleted();
        ClearDisturbances();
    }
}

/// <summary>
/// An aggregator that does a fold (reduction) over all values using the provided monoidal addition function.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class DisturbedFold<T> : DisturbedEvented<T> {
    private readonly Func<T, T, T> folder;
    /// <summary>
    /// </summary>
    public DisturbedFold(T val, Func<T, T, T> folder) : base(val) {
        this.folder = folder;
    }
    /// <inheritdoc/>
    protected override T Fold(T acc, T next) => folder(acc, next);
}

/// <summary>
/// Get the most recent value.
/// </summary>
[PublicAPI]
public class OverrideEvented<T> : DisturbedEvented<T> {
    /// <summary>
    /// </summary>
    public OverrideEvented(T val) : base(val) { }
    /// <inheritdoc/>
    protected override T Fold(T acc, T next) => next;
}

/// <summary>
/// Get the sum of all values.
/// </summary>
[PublicAPI]
public class DisturbedSum<T> : DisturbedEvented<T> {
    private static readonly (T zero, Func<T, T, T> add) monoid = GenericOps.GetAddOp<T>();
    /// <summary>
    /// Get the sum of all values with the monoidal zero.
    /// </summary>
    public DisturbedSum() : base(monoid.zero) { }
    /// <summary>
    /// Get the sum of all values with the provided base value.
    /// </summary>
    public DisturbedSum(T val) : base(val) { }

    /// <summary>
    /// Get the sum of all values with the provided base value.
    /// </summary>
    public DisturbedSum(IBObservable<T> val) : base(monoid.zero) {
        AddDisturbance(val);
    }
    /// <inheritdoc/>
    protected override T Fold(T acc, T next) => monoid.add(acc, next);
}
/// <summary>
/// Get the product of all values.
/// </summary>
[PublicAPI]
public class DisturbedProduct<T> : DisturbedEvented<T> {
    private static readonly (T zero, Func<T, T, T> add) monoid = GenericOps.GetVecMulOp<T>();
    
    /// <summary>
    /// Get the product of all values with the monoidal zero.
    /// </summary>
    public DisturbedProduct() : base(monoid.zero) { }
    /// <summary>
    /// Get the product of all values with the provided base value.
    /// </summary>
    public DisturbedProduct(T val) : base(val) { }
    
    /// <summary>
    /// Get the product of all values with the provided base value.
    /// </summary>
    public DisturbedProduct(IBObservable<T> val) : base(monoid.zero) {
        AddDisturbance(val);
    }
    /// <inheritdoc/>
    protected override T Fold(T acc, T next) => monoid.add(acc, next);
}

/// <summary>
/// Get the AND of all provided values (true when all values are true).
/// </summary>
[PublicAPI]
public class DisturbedAnd : DisturbedEvented<bool> {
    /// <summary>
    /// </summary>
    /// <param name="first">Initial value (true by default)</param>
    public DisturbedAnd(bool first=true) : base(first) { }
    
    /// <inheritdoc/>
    protected override bool Fold(bool acc, bool next) => acc && next;
}

/// <summary>
/// Get the OR of all provided values (true when any value is true).
/// </summary>
[PublicAPI]
public class DisturbedOr : DisturbedEvented<bool> {
    /// <summary>
    /// </summary>
    /// <param name="first">Initial value (false by default)</param>
    public DisturbedOr(bool first=false) : base(first) { }
    
    /// <inheritdoc/>
    protected override bool Fold(bool acc, bool next) => acc || next;
}


}