using System;
using BagoumLib.Mathematics;

namespace BagoumLib.Events {

public class PushLerper<T> {
    private readonly Func<T, T, float, T> lerper;
    private readonly float lerpTime;

    private bool set = false;
    private T lastValue;
    private T nextValue;
    private float elapsed;

    public Evented<T> OnChange { get; }
    public T Value => OnChange.Value;
    
    public PushLerper(float lerpTime, Func<T, T, float, T> lerper) {
        this.lerpTime = lerpTime;
        this.lerper = lerper;
        this.OnChange = new(this.lastValue = nextValue = default!);
    }

    public void Push(T targetValue) {
        if (set) {
            lastValue = Value;
            nextValue = targetValue;
            OnChange.Value = lerper(lastValue, nextValue, 0);
        } else {
            OnChange.Value = lastValue = nextValue = targetValue;
        }
        set = true;
        elapsed = 0;
    }

    public void Update(float dT) {
        if (elapsed < lerpTime) {
            elapsed += dT;
            OnChange.Value = lerper(lastValue, nextValue, BMath.Clamp(0, 1, elapsed / lerpTime));
        }
    }

    public static implicit operator T(PushLerper<T> pl) => pl.Value;
}
}