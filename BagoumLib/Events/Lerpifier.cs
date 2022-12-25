using System;
using System.Reactive.Subjects;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// Lerpifier tracks a changing value (targetValue) by smoothly lerping to it.
/// </summary>
[PublicAPI]
public class Lerpifier<T> : ICObservable<T> where T : IComparable<T> {
    private readonly Func<T, T, float, T> lerper;
    private readonly Func<T> targetValue;
    private readonly float lerpTime;
    private T lastSourceValue;
    private T lastTargetValue;
    /// <inheritdoc/>
    public T Value => OnChange.Value;
    private float elapsed;

    private Evented<T> OnChange { get; }

    /// <summary>
    /// Create a new <see cref="Lerpifier{T}"/>.
    /// </summary>
    /// <param name="lerper">Lerping function.</param>
    /// <param name="targetValue">Initial target function.</param>
    /// <param name="lerpTime">Time it should take to lerp between an existing value and a new target function.</param>
    public Lerpifier(Func<T, T, float, T> lerper, Func<T> targetValue, float lerpTime) {
        this.lerper = lerper;
        this.targetValue = targetValue;
        this.lerpTime = lerpTime;
        this.elapsed = this.lerpTime;
        this.lastSourceValue = lastTargetValue = targetValue();
        this.OnChange = new Evented<T>(lastTargetValue);
    }

    /// <summary>
    /// Reset the state of the container as if it was initialized with the current <see cref="targetValue"/>.
    /// </summary>
    public void HardReset() {
        this.elapsed = this.lerpTime;
        this.lastSourceValue = lastTargetValue = OnChange.Value = targetValue();
    }

    /// <summary>
    /// Update the state of the container.
    /// </summary>
    /// <param name="dt">Time passed since last update</param>
    public void Update(float dt) {
        var nextTarget = targetValue();
        if (nextTarget.CompareTo(lastTargetValue) != 0) {
            lastSourceValue = OnChange.Value;
            lastTargetValue = nextTarget;
            elapsed = 0;
        }
        elapsed += dt;
        var nxt = elapsed >= lerpTime ? 
            lastTargetValue : 
            lerper(lastSourceValue, lastTargetValue, lerpTime <= 0 ? 1 : (elapsed / lerpTime));
        if (OnChange.Value.CompareTo(nxt) != 0)
            OnChange.Value = nxt;
    }
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer) => OnChange.Subscribe(observer);

    /// <summary>
    /// Get the current value of the container.
    /// </summary>
    public static implicit operator T(Lerpifier<T> lerpifier) => lerpifier.Value;
}
}