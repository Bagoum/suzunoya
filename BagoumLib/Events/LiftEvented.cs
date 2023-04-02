using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace BagoumLib.Events {
/// <summary>
/// A lifted binary function over <see cref="BagoumLib.Events.Evented{T}"/>.
/// </summary>
public class LiftEvented<A, B, R> : ICObservable<R>, ITokenized {
    /// <inheritdoc />
    public List<IDisposable> Tokens { get; } = new();
    private readonly Func<A, B, R> merge;
    private readonly ICObservable<A> a;
    private readonly ICObservable<B> b;

    /// <inheritdoc />
    public R Value => onSet.Value;

    private readonly Evented<R> onSet;

    /// <summary>
    /// The initial value is published to onSet.
    /// </summary>
    public LiftEvented(Func<A, B, R> merge, ICObservable<A> a, ICObservable<B> b) {
        this.merge = merge;
        this.a = a;
        this.b = b;
        onSet = new Evented<R>(merge(a.Value, b.Value));
        Tokens.Add(a.Subscribe(_ => Reevaluate()));
        Tokens.Add(b.Subscribe(_ => Reevaluate()));
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<R> observer) {
        observer.OnNext(Value);
        return onSet.Subscribe(observer);
    }

    /// <summary>
    /// Re-evaluate the arguments and publish the result.
    /// </summary>
    public void Reevaluate() {
        var value = merge(a.Value, b.Value);
        if (!EqualityComparer<R>.Default.Equals(value, Value))
            onSet.OnNext(value);
    }

    /// <summary>
    /// Gets <see cref="Value"/>.
    /// </summary>
    public static implicit operator R(LiftEvented<A, B, R> evo) => evo.Value;
}
}