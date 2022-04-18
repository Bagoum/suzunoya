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
public class PushLerper<T> : ICObservable<T> {
    private readonly Func<T, T, float, T> lerper;
    private readonly Func<T, T, float> lerpTime;

    private bool set = false;
    private T prevValue;
    private T nextValue;
    private float elapsed;
    private float ElapsedRatio(float lt) => BMath.Clamp(0, 1, lt <= 0 ? 1 : (elapsed / lt));

    private Evented<T> OnChange { get; }
    public T Value => OnChange.Value;

    public PushLerper(float lerpTime, Func<T, T, float, T>? lerper = null) : this((a, b) => lerpTime, lerper) { }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="lerpTime">A pure function that returns the lerp time given the previous and next values.</param>
    /// <param name="lerper"></param>
    public PushLerper(Func<T, T, float> lerpTime, Func<T, T, float, T>? lerper = null) {
        this.lerpTime = lerpTime;
        elapsed = lerpTime(default!, default!);
        this.lerper = lerper ?? GenericOps.GetLerp<T>();
        this.OnChange = new(this.prevValue = nextValue = default!);
    }

    public void Push(T targetValue, float initTime = 0) {
        if (set) {
            prevValue = Value;
            nextValue = targetValue;
            elapsed = initTime;
            OnChange.Value = lerper(prevValue, nextValue, ElapsedRatio(lerpTime(prevValue, nextValue)));
        } else {
            elapsed = initTime;
            OnChange.Value = prevValue = nextValue = targetValue;
        }
        set = true;
    }

    public void PushIfNotSame(T targetValue, float initTime = 0) {
        if (!set || !Equals(nextValue, targetValue))
            Push(targetValue, initTime);
    }

    public void Update(float dT) {
        var lt = lerpTime(prevValue, nextValue);
        if (elapsed < lt) {
            elapsed += dT;
            OnChange.Value = lerper(prevValue, nextValue, ElapsedRatio(lt));
        }
    }

    /// <summary>
    /// Puts the object in a state such that the next time a value is pushed, it will be instantaneously lerped to.
    /// </summary>
    public void Unset() {
        elapsed = lerpTime(prevValue, nextValue);
        set = false;
    }

    public static implicit operator T(PushLerper<T> pl) => pl.Value;
    public IDisposable Subscribe(IObserver<T> observer) => OnChange.Subscribe(observer);

}
}