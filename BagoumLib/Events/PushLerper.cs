using System;
using System.Collections.Generic;
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
    private Func<T, T, float, T>? _lerper;
    //NB: delaying the query to GenericOps helps with allowing custom types to be registered
    // in Unity, where class fields on MonoBehaviors are created before the scene is executed and Awake is called.
    private Func<T, T, float, T> Lerper => _lerper ??= GenericOps.GetLerp<T>();
    /// <summary>
    /// Function that determines how much time it takes to lerp from the previous value (first argument)
    ///  to the next value (second argument).
    /// </summary>
    public Func<T, T, float> LerpTime { get; private set; }

    private bool set = false;
    private T prevValue;
    private T nextValue;
    private float elapsed;
    private float ElapsedRatio(float lt) => BMath.Clamp(0, 1, lt <= 0 ? 1 : (elapsed / lt));
    private float LerpController01 => ElapsedRatio(LerpTime(prevValue, nextValue));
    /// <summary>
    /// True iff the lerp process has attained its endpoint.
    /// </summary>
    public bool IsSteadyState => !set || elapsed >= LerpTime(prevValue, nextValue);

    private Evented<T> OnChange { get; }
    
    /// <inheritdoc/>
    public T Value => OnChange.Value;

    /// <inheritdoc/>
    public PushLerper(float lerpTime, Func<T, T, float, T>? lerper = null) : this((a, b) => lerpTime, lerper) { }
    
    /// <summary>
    /// </summary>
    /// <param name="lerpTime">A pure function that returns the lerp time given the previous and next values.</param>
    /// <param name="lerper"></param>
    public PushLerper(Func<T, T, float> lerpTime, Func<T, T, float, T>? lerper = null) {
        this.LerpTime = lerpTime;
        elapsed = lerpTime(default!, default!);
        this._lerper = lerper;
        this.OnChange = new(this.prevValue = nextValue = default!);
    }

    /// <summary>
    /// Set a new target value to lerp to.
    /// </summary>
    /// <param name="targetValue">The new target value.</param>
    /// <param name="initTime">The time that the lerp between the current value and the new target should start at.</param>
    public void Push(T targetValue, float initTime = 0) {
        if (set) {
            prevValue = Value;
            nextValue = targetValue;
            elapsed = initTime;
            OnChange.Value = Lerper(prevValue, nextValue, LerpController01);
        } else {
            OnChange.Value = prevValue = nextValue = targetValue;
            elapsed = LerpTime(prevValue, nextValue);
        }
        set = true;
    }

    /// <summary>
    /// Set a new target value to lerp to (see <see cref="Push"/>) if it is not the same as the existing target value.
    /// </summary>
    public void PushIfNotSame(T targetValue, float initTime = 0) {
        if (!set || !EqualityComparer<T>.Default.Equals(nextValue, targetValue))
            Push(targetValue, initTime);
    }
    
    /// <summary>
    /// Force-set the current value to `targetValue` if it is not the same as the existing target value.
    /// </summary>
    public void ForcePushIfNotSame(T targetValue, float initTime = 0) {
        if (!set || !EqualityComparer<T>.Default.Equals(nextValue, targetValue)) {
            Unset();
            Push(targetValue, initTime);
        }
    }

    /// <summary>
    /// Update the lerp process for a given delta-time.
    /// </summary>
    public void Update(float dT) {
        var lt = LerpTime(prevValue, nextValue);
        if (elapsed < lt) {
            elapsed += dT;
            OnChange.Value = Lerper(prevValue, nextValue, ElapsedRatio(lt));
        }
    }

    /// <summary>
    /// Change the function that determines how long it takes to lerp between existing and target values.
    /// </summary>
    public void ChangeLerpTime(Func<T, T, float> newLerpTime) {
        this.LerpTime = newLerpTime;
        if (set)
            OnChange.Value = Lerper(prevValue, nextValue, LerpController01);
    }

    /// <summary>
    /// Puts the object in a state such that the next time a value is pushed, it will be instantaneously lerped to.
    /// </summary>
    public void Unset() {
        elapsed = LerpTime(prevValue, nextValue);
        set = false;
    }

    /// <summary>
    /// Get the current value of a <see cref="PushLerper{T}"/>.
    /// </summary>
    public static implicit operator T(PushLerper<T> pl) => pl.Value;
    
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer) => OnChange.Subscribe(observer);

}
}