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
    public T Value => OnChange.Value;
    private float elapsed;

    private Evented<T> OnChange { get; }

    public Lerpifier(Func<T, T, float, T> lerper, Func<T> targetValue, float lerpTime) {
        this.lerper = lerper;
        this.targetValue = targetValue;
        this.lerpTime = lerpTime;
        this.elapsed = this.lerpTime;
        this.lastSourceValue = lastTargetValue = targetValue();
        this.OnChange = new Evented<T>(lastTargetValue);
    }

    public void HardReset() {
        this.elapsed = this.lerpTime;
        this.lastSourceValue = lastTargetValue = OnChange.Value = targetValue();
    }

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

    public IDisposable Subscribe(IObserver<T> observer) => OnChange.Subscribe(observer);

    public static implicit operator T(Lerpifier<T> lerpifier) => lerpifier.Value;
}
}