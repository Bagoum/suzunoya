using System;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib.Events {

/// <summary>
/// A wrapper around a sequence of values that smoothly lerps to the most recent value provided.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class PushLerper<T> : IBObservable<T> {
    private readonly Func<T, T, float, T> lerper;
    private readonly float lerpTime;

    private bool set = false;
    private T prevValue;
    private T nextValue;
    private float elapsed;
    private float ElapsedRatio => BMath.Clamp(0, 1, lerpTime <= 0 ? 1 : (elapsed / lerpTime));

    private Evented<T> OnChange { get; }
    public T Value => OnChange.Value;
    public Maybe<T> LastPublished => OnChange.LastPublished;
    
    public PushLerper(float lerpTime, Func<T, T, float, T> lerper) {
        this.lerpTime = elapsed = lerpTime;
        this.lerper = lerper;
        this.OnChange = new(this.prevValue = nextValue = default!);
    }

    public void Push(T targetValue, float initTime = 0) {
        if (set) {
            prevValue = Value;
            nextValue = targetValue;
            elapsed = initTime;
            OnChange.Value = lerper(prevValue, nextValue, ElapsedRatio);
        } else {
            elapsed = initTime;
            OnChange.Value = prevValue = nextValue = targetValue;
        }
        set = true;
    }

    public void Update(float dT) {
        if (elapsed < lerpTime) {
            elapsed += dT;
            OnChange.Value = lerper(prevValue, nextValue, ElapsedRatio);
        }
    }

    /// <summary>
    /// Puts the object in a state such that the next time a value is pushed, it will be instantaneously lerped to.
    /// </summary>
    public void Unset() {
        elapsed = lerpTime;
        set = false;
    }

    public static implicit operator T(PushLerper<T> pl) => pl.Value;
    public IDisposable Subscribe(IObserver<T> observer) => OnChange.Subscribe(observer);

}
}