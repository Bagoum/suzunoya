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
public class PushLerperF<T> : IBObservable<T> {
    private readonly Func<T, T, float, T> lerper;
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
    public T Value => OnChange.Value;
    public Maybe<T> LastPublished => OnChange.LastPublished;
    
    public PushLerperF(float lerpTime, Func<T, T, float, T> lerper) {
        this.lerpTime = lerpTime;
        this.lerper = lerper;
        this.prevFunc = this.nextFunc = t => default!;
        this.OnChange = new(default!);
    }

    public void Push(Func<float, T> targetValue, float initTime = 0) {
        if (set) {
            prevFunc = (elapsedNext < lerpTime && prevFunc != nextFunc) ? _ => Value : nextFunc;
            nextFunc = targetValue;
            elapsedPrev = elapsedNext;
            elapsedNext = initTime;
            OnChange.Value = lerper(PrevValue, NextValue, ElapsedRatio);
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

    public void Update(float dT) {
        elapsedPrev += dT;
        elapsedNext += dT;
        OnChange.Value = lerper(PrevValue, NextValue, ElapsedRatio);
    }

    public static implicit operator T(PushLerperF<T> pl) => pl.Value;
    public IDisposable Subscribe(IObserver<T> observer) => OnChange.Subscribe(observer);

}
}