using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using JetBrains.Annotations;
using static BagoumLib.Tweening.Tween;

namespace BagoumLib.Tweening {
[PublicAPI]
public interface ITweener {
    Task Run(ICoroutineRunner cors);
}

[PublicAPI]
public static class Tween {
    public static Func<float>? DefaultDeltaTimeProvider { get; set; }
    
    private static readonly Dictionary<Type, object> lerpers = new Dictionary<Type, object>() {
        {typeof(float), (Func<float, float, float, float>) BMath.Lerp},
        {typeof(Vector2), (Func<Vector2, Vector2, float, Vector2>) Vector2.Lerp},
        {typeof(Vector3), (Func<Vector3, Vector3, float, Vector3>) Vector3.Lerp},
        {typeof(Vector4), (Func<Vector4, Vector4, float, Vector4>) Vector4.Lerp},
    };
    private static readonly Dictionary<Type, object> multiplyOps = new Dictionary<Type, object>() {
        {typeof(float), (Func<float, float, float>) ((x, y) => x * y)},
        {typeof(Vector2), (Func<Vector2, float, Vector2>) ((x, y) => x * y)},
        {typeof(Vector3), (Func<Vector3, float, Vector3>) ((x, y) => x * y)},
        {typeof(Vector4), (Func<Vector4, float, Vector4>) ((x, y) => x * y)},
    };
    public static Func<T, T, float, T> GetLerp<T>() => lerpers.TryGetValue(typeof(T), out var l) ?
        (Func<T, T, float, T>)l :
        throw new Exception($"No lerp handling for type {l}");
    public static Func<T, float, T> GetMulOp<T>() => multiplyOps.TryGetValue(typeof(T), out var l) ?
        (Func<T, float, T>)l :
        throw new Exception($"No multiply handling for type {l}");

    public static void RegisterLerper<T>(Func<T, T, float, T> lerper) => lerpers[typeof(T)] = lerper;
    public static void RegisterMultiplier<T>(Func<T, float, T> mulOp) => multiplyOps[typeof(T)] = mulOp;

    public static void RegisterType<T>(Func<T, T, float, T> lerper, Func<T, float, T> mulOp) {
        RegisterLerper(lerper);
        RegisterMultiplier(mulOp);
    }

    public static Tweener<T> TweenTo<T>(T start, T end, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        new(start, end, time, apply, ease, cT);

    public static Tweener<T> TweenBy<T>(T start, float by, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        TweenTo(start, GetMulOp<T>()(start, by), time, apply, ease, cT);

    public static ITweener Then(this ITweener tw, ITweener next) => new SequentialTweener(tw, next);
    public static ITweener Parallel(params ITweener[] tws) => new ParallelTweener(tws);
    public static ITweener Loop(this ITweener tw) => new LoopTweener(tw);
}

[PublicAPI]
public record Tweener<T> : ITweener {
    //Required variables
    /// <summary>
    /// Initial value of tweening.
    /// </summary>
    public T Start { get; init; }
    /// <summary>
    /// Final value of tweening.
    /// </summary>
    public T End { get; init; }
    /// <summary>
    /// Amount of time over which to perform tweening.
    /// </summary>
    public float Time { get; init; }
    /// <summary>
    /// Method to update tweened value.
    /// </summary>
    public Action<T> Apply { get; init; }
    //Required variables with defaults provided
    
    /// <summary>
    /// Cancellation token used to stop execution.
    /// If set to null, then will use the RunDroppable interface on the executing coroutine manager.
    /// </summary>
    public ICancellee? CToken { get; init; }

    //Optional variables set via initializer syntax (or possibly fluent API)
    /// <summary>
    /// Lerp function specific to type T. Add handling for types via Tween.RegisterLerper.
    /// </summary>
    private Func<T, T, float, T> Lerp { get; } = Tween.GetLerp<T>();
    /// <summary>
    /// Method to retrieve the delta-time of the current frame. Preferably set this via Tween.DefaultDeltaTimeProvider.
    /// </summary>
    public Func<float> DeltaTimeProvider { get; init; } = DefaultDeltaTimeProvider!;
    /// <summary>
    /// Easing method used to smooth the tweening process. By default, set to IOSine (not Linear).
    /// </summary>
    public Easer Ease { get; init; } = Easers.EIOSine;
    /// <summary>
    /// When cancelled, the tween will also apply the End value if this flag is true.
    /// Whether or not you should enable this depends on the semantics of cancelling.
    /// <br/>If cancelling has the semantics of "skip animation", then you should set it to true.
    /// <br/>If cancelling has the semantics of "animation has been superseded", then you should set it to false. 
    /// </summary>
    public bool SetFinalOnCancel { get; init; } = false;

    //Fluent methods for consumers without c#9 access
    public Tweener<T> WithEaser(Easer? e) => this with {Ease = e ?? Ease };

    public Tweener(T start, T end, float time, Action<T> apply, Easer? ease = null, ICancellee? cT = null) {
        Start = start;
        End = end;
        Time = time;
        Apply = apply;
        CToken = cT;
        Ease = ease ?? Ease;
    }

    private float DeltaTime => (DeltaTimeProvider ?? throw new Exception(
        "No delta time provider has been set! It is recommended to set TweenHelpers.DefaultDeltaTimeProvider."))();


    private IEnumerator RunIEnum(Action<Completion> done) {
        for (float t = 0; t < Time; t += DeltaTime) {
            if (CToken?.Cancelled == true) 
                break;
            Apply(Lerp(Start, End, Ease(t / Time)));
            yield return null;
        }
        if (SetFinalOnCancel || CToken?.Cancelled != true)
            Apply(End);
        done(CToken?.Cancelled == true ? Completion.Cancelled : Completion.Standard);
    }

    public Task Run(ICoroutineRunner cors) {
        Apply(Start);
        var ienum = RunIEnum(WaitingUtils.GetCompletionAwaiter(out var t));
        if (CToken != null)
            cors.Run(ienum);
        else
            cors.RunDroppable(ienum);
        return t;
    }

    public Tweener<T> Reverse(bool reverseEase = true) => 
        new(End, Start, Time, Apply, reverseEase ? new Easer(t => 1 - Ease(1 - t)) : Ease, CToken) {
            DeltaTimeProvider = DeltaTimeProvider,
            SetFinalOnCancel = SetFinalOnCancel
        };
}

public class SequentialTweener : ITweener {
    private readonly ITweener[] states;
    public SequentialTweener(params ITweener[] states) {
        this.states = states;
    }

    public async Task Run(ICoroutineRunner cors) {
        for (int ii = 0; ii < states.Length; ++ii)
            await states[ii].Run(cors);
    }
}

public class ParallelTweener : ITweener {
    private readonly ITweener[] states;
    public ParallelTweener(params ITweener[] states) {
        this.states = states;
    }

    public Task Run(ICoroutineRunner cors) {
        if (states.Length == 1)
            return states[0].Run(cors);
        return Task.WhenAll(states.Select(s => s.Run(cors)));
    }
}

public class LoopTweener : ITweener {
    private readonly ITweener subj;
    public LoopTweener(ITweener subj) {
        this.subj = subj;
    }

    public async Task Run(ICoroutineRunner cors) {
        while (true)
            await subj.Run(cors);
        // ReSharper disable once FunctionNeverReturns
    }
}

}