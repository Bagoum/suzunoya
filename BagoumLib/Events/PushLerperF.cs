using System;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib.Events {

/// <summary>
/// A wrapper around a sequence of single-argument functions that smoothly lerps to the most recent function.
/// <br/>The functions are evaluated with the time-since-push as the single argument.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class PushLerperF<T> : ICObservable<T> {
    private Func<T, T, float, T>? _lerper;
    private Func<T, T, float, T> Lerper => _lerper ??= GenericOps.GetLerp<T>();
    private readonly float lerpTime;

    private bool set = false;
    private Func<float, T> prevFunc;
    private float elapsedPrev = 0;
    private T PrevValue => prevFunc(elapsedPrev);
    private Func<float, T> nextFunc;
    private float elapsedNext = 0;
    private T NextValue => nextFunc(elapsedNext);
    private float ElapsedRatio => BMath.Clamp(0, 1, lerpTime <= 0 ? 1 : (elapsedNext / lerpTime));

    private Evented<T> OnChange { get; }
    
    /// <inheritdoc/>
    public T Value => OnChange.Value;
    
    /// <inheritdoc cref="PushLerperF{T}"/>
    public PushLerperF(float lerpTime, Func<T, T, float, T>? lerper = null) {
        this.lerpTime = lerpTime;
        this._lerper = lerper;
        this.prevFunc = this.nextFunc = t => default!;
        this.OnChange = new(default!);
    }

    /// <inheritdoc cref="PushLerper{T}.Push"/>
    public void Push(Func<float, T> targetValue, float initTime = 0) {
        if (set) {
            prevFunc = (elapsedNext < lerpTime && prevFunc != nextFunc) ? _ => Value : nextFunc;
            nextFunc = targetValue;
            elapsedPrev = elapsedNext;
            elapsedNext = initTime;
            OnChange.Value = Lerper(PrevValue, NextValue, ElapsedRatio);
        } else {
            prevFunc = nextFunc = targetValue;
            elapsedPrev = elapsedNext = initTime;
            OnChange.Value = PrevValue;
        }
        set = true;
    }

    /// <summary>
    /// Puts the object in a state such that the next time a function is pushed, it will be instantaneously lerped to.
    /// </summary>
    public void Unset() {
        set = false;
    }

    /// <inheritdoc cref="PushLerper{T}.Update"/>
    public void Update(float dT) {
        elapsedPrev += dT;
        elapsedNext += dT;
        OnChange.Value = Lerper(PrevValue, NextValue, ElapsedRatio);
    }

    /// <summary>
    /// Get the current value of a <see cref="PushLerper{T}"/>.
    /// </summary>
    public static implicit operator T(PushLerperF<T> pl) => pl.Value;
    
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer) => OnChange.Subscribe(observer);

}
}