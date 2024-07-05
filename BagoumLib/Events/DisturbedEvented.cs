using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// <see cref="DisturbedEvented{D,V}"/> with only the first type parameter (type of disturbance effect).
/// </summary>
public interface IDisturbable<D> {
    /// <summary>
    /// Add a disturbance.
    /// Whenver the disturbance receives a new value, this aggregate will update as well.
    /// </summary>
    IDisposable AddDisturbance(IBObservable<D> disturbance);
    
    /// <summary>
    /// Add a constant value disturbance.
    /// This aggregate will also update immediately.
    /// </summary>
    IDisposable AddConst(D value);
}

/// <summary>
/// DisturbedEvented is a wrapper around a core value (eg. a location) that can be modified by any number
///  of disturbance effects (eg. shake effects) that all use the same aggregation function (eg. addition).
/// <br/>Note that <see cref="get_Value"/> and the event listeners
///  will return the total value, whereas <see cref="set_Value"/> will set the core value.
/// <br/>Use <see cref="get_BaseValue"/> to get the core value. <see cref="set_BaseValue"/>
///  will also set the core value.
/// <br/>Note that, like <see cref="Evented{T}"/>, new subscribers will be sent the current value immediately.
/// </summary>
/// <typeparam name="D">Type of disturbance effect.</typeparam>
/// <typeparam name="V">Type of output value.</typeparam>
[PublicAPI]
public abstract class DisturbedEvented<D, V> : IDisturbable<D>, ICSubject<V, V> {
    private V _baseValue;
    /// <summary>
    /// Initial (aggregation seed) value.
    /// </summary>
    public V BaseValue {
        get => _baseValue;
        set {
            this._baseValue = value;
            DoPublishIfNotSame();
        }
    }
    private V ComputeValue() {
        var agg = _baseValue;
        Disturbances.Compact();
        for (int ii = 0; ii < Disturbances.Count; ++ii)
            if (Disturbances[ii].HasValue)
                agg = Fold(agg, Disturbances[ii].Value);
        return agg;
    }
    
    /// <summary>
    /// Aggregate value. Note that the set operator modifies <see cref="BaseValue"/>.
    /// </summary>
    public V Value {
        get => onSet.Value;
        set => BaseValue = value;
    }

    private readonly Evented<V> onSet;
    
    /// <summary>
    /// List of disturbance effects applied to the core value.
    /// </summary>
    public DMCompactingArray<IBObservable<D>> Disturbances { get; } = new();

    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    public DisturbedEvented(V val) {
        _baseValue = val;
        onSet = new Evented<V>(ComputeValue());
    }

    /// <summary>
    /// Get the aggregate value.
    /// </summary>
    public static implicit operator V(DisturbedEvented<D, V> evo) => evo.Value;

    /// <summary>
    /// Function that aggregates values.
    /// </summary>
    /// <param name="acc">Accumulated value</param>
    /// <param name="next">Next value to fold into acc</param>
    /// <returns></returns>
    protected abstract V Fold(V acc, D next);
    
    /// <inheritdoc/>
    public IDisposable AddDisturbance(IBObservable<D> disturbance) {
        var trackToken = Disturbances.Add(disturbance);
        var updateToken = disturbance.Subscribe(_ => DoPublishIfNotSame());
        return new JointDisposable(DoPublishIfNotSame, trackToken, updateToken);
    }

    /// <inheritdoc/>
    public IDisposable AddConst(D value) {
        var trackToken = Disturbances.Add(new ConstantObservable<D>(value));
        DoPublishIfNotSame();
        return new JointDisposable(DoPublishIfNotSame, trackToken);
    }

    /// <summary>
    /// Copy the disturbances from another <see cref="DisturbedEvented{T,U}"/>.
    /// The copied disturbances will be bound by the same <see cref="IDisposable"/>s as the original.
    /// </summary>
    /// <param name="src"></param>
    public void CopyDisturbances(DisturbedEvented<D,V> src) {
        for (int ii = 0; ii < src.Disturbances.Count; ++ii)
            if (src.Disturbances.GetMarkerIfExistsAt(ii, out var m)) {
                m.DisallowPooling();
                Disturbances.AddPriority(m);
            }
        DoPublishIfNotSame();
    }

    private void DoPublishIfNotSame() {
        var nxt = ComputeValue();
        if (EqualityComparer<V>.Default.Equals(nxt, Value))
            return;
        onSet.OnNext(nxt);
    }

    /// <summary>
    /// Remove all additional provided values. 
    /// </summary>
    public void ClearDisturbances() {
        Disturbances.Empty();
        DoPublishIfNotSame();
    }
    
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<V> observer) => onSet.Subscribe(observer);

    /// <inheritdoc/>
    public void OnNext(V value) => BaseValue = value;

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
[PublicAPI]
public class DisturbedFold<D> : DisturbedEvented<D, D> {
    private readonly Func<D, D, D> folder;
    /// <summary>
    /// </summary>
    public DisturbedFold(D val, Func<D, D, D> folder) : base(val) {
        this.folder = folder;
    }
    /// <inheritdoc/>
    protected override D Fold(D acc, D next) => folder(acc, next);
}

/// <summary>
/// An aggregator that does a fold (reduction) over all values using the provided combination function.
/// </summary>
[PublicAPI]
public class DisturbedFold<D, V> : DisturbedEvented<D, V> {
    private readonly Func<V, D, V> folder;
    /// <summary>
    /// </summary>
    public DisturbedFold(V val, Func<V, D, V> folder) : base(val) {
        this.folder = folder;
    }
    /// <inheritdoc/>
    protected override V Fold(V acc, D next) => folder(acc, next);
}

/// <summary>
/// Interface for elements that can be combined in <see cref="DisturbedAggregation{D}"/>.
/// </summary>
/// <typeparam name="D"></typeparam>
public interface IAggregator<D> {
    /// <summary>
    /// Fold this value onto the accumulated value.
    /// </summary>
    D FoldOnto(D acc);
}

/// <summary>
/// An element that can be combined in <see cref="DisturbedAggregation{D}"/>.
/// </summary>
public record Aggregator<D, I> : IAggregator<D> {
    /// <summary>
    /// Data contained in this element.
    /// </summary>
    public I Data { get; set; }
    /// <summary>
    /// Method for combining this element's data with the accumulator.
    /// </summary>
    public Func<D,I,D> Folder { get; set; }
    
    /// <inheritdoc cref="Aggregator{D,I}"/>
    public Aggregator(I Data, Func<D,I,D> Folder) {
        this.Data = Data;
        this.Folder = Folder;
    }

    /// <inheritdoc/>
    public D FoldOnto(D acc) => Folder(acc, Data);
}

/// <summary>
/// An aggregator that folds functions of type T->T onto a base value of type T.
/// </summary>
[PublicAPI]
public class DisturbedAggregation<D> : DisturbedEvented<IAggregator<D>, D> {
    /// <summary>
    /// </summary>
    public DisturbedAggregation(D val) : base(val) {
    }

    /// <inheritdoc/>
    protected override D Fold(D acc, IAggregator<D> next) => next.FoldOnto(acc);
}

/// <summary>
/// Get the most recent value.
/// </summary>
[PublicAPI]
public class OverrideEvented<D> : DisturbedEvented<D, D> {
    /// <summary>
    /// </summary>
    public OverrideEvented(D val) : base(val) { }
    /// <inheritdoc/>
    protected override D Fold(D acc, D next) => next;
}

/// <summary>
/// Get the sum of all values.
/// </summary>
[PublicAPI]
public class DisturbedSum<T> : DisturbedEvented<T, T> {
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
public class DisturbedProduct<T> : DisturbedEvented<T, T> {
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
public class DisturbedAnd : DisturbedEvented<bool, bool> {
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
public class DisturbedOr : DisturbedEvented<bool, bool> {
    /// <summary>
    /// </summary>
    /// <param name="first">Initial value (false by default)</param>
    public DisturbedOr(bool first=false) : base(first) { }
    
    /// <inheritdoc/>
    protected override bool Fold(bool acc, bool next) => acc || next;
}


}