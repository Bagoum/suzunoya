using System;
using System.Reactive.Subjects;
using JetBrains.Annotations;

namespace BagoumLib.Events {
/// <summary>
/// Lerpifier tracks a changing value (targetValue) by smoothly lerping to it.
/// </summary>
[PublicAPI]
public class Lerpifier<T> : IObservable<T> where T : IComparable<T> {
    private readonly Func<T, T, float, T> lerper;
    private readonly Func<T> targetValue;
    private readonly float lerpTime;
    private T lastSourceValue;
    private T lastTargetValue;
    public T Value => ev.Value;
    private float elapsed;

    private readonly Evented<T> ev;

    public Lerpifier(Func<T, T, float, T> lerper, Func<T> targetValue, float lerpTime) {
        this.lerper = lerper;
        this.targetValue = targetValue;
        this.lerpTime = lerpTime;
        this.elapsed = this.lerpTime;
        this.lastSourceValue = lastTargetValue = targetValue();
        this.ev = new Evented<T>(lastTargetValue);
    }

    public void HardReset() {
        this.elapsed = this.lerpTime;
        this.lastSourceValue = lastTargetValue = ev.Value = targetValue();
    }

    public void Update(float dt) {
        var nextTarget = targetValue();
        if (nextTarget.CompareTo(lastTargetValue) != 0) {
            lastSourceValue = ev.Value;
            lastTargetValue = nextTarget;
            elapsed = 0;
        }
        elapsed += dt;
        var nxt = elapsed >= lerpTime ? 
            lastTargetValue : 
            lerper(lastSourceValue, lastTargetValue, elapsed / lerpTime);
        if (ev.Value.CompareTo(nxt) != 0)
            ev.Value = nxt;
    }

    public IDisposable Subscribe(IObserver<T> observer) => ev.Subscribe(observer);

    public static implicit operator T(Lerpifier<T> lerpifier) => lerpifier.Value;
}
}