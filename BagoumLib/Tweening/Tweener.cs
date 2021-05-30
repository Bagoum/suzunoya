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
    /// <summary>
    /// Runs the tweener.
    /// </summary>
    Task<Completion> Run(ICoroutineRunner cors);

    /// <summary>
    /// Nondestructively modify all nested tweeners.
    /// </summary>
    ITweener With(ICancellee cT, Func<float> dTProvider);
}

[PublicAPI]
public static class Tween {
    public static Func<float>? DefaultDeltaTimeProvider { get; set; }
    
    private static readonly Dictionary<Type, object> lerpers = new() {
        {typeof(float), (Func<float, float, float, float>) BMath.Lerp},
        {typeof(Vector2), (Func<Vector2, Vector2, float, Vector2>) Vector2.Lerp},
        {typeof(Vector3), (Func<Vector3, Vector3, float, Vector3>) Vector3.Lerp},
        {typeof(Vector4), (Func<Vector4, Vector4, float, Vector4>) Vector4.Lerp},
        {typeof(FColor), (Func<FColor, FColor, float, FColor>) FColor.Lerp},
    };
    private static readonly Dictionary<Type, object> multiplyOps = new() {
        {typeof(float), (Func<float, float, float>) ((x, y) => x * y)},
        {typeof(Vector2), (Func<Vector2, float, Vector2>) ((x, y) => x * y)},
        {typeof(Vector3), (Func<Vector3, float, Vector3>) ((x, y) => x * y)},
        {typeof(Vector4), (Func<Vector4, float, Vector4>) ((x, y) => x * y)},
        {typeof(FColor), (Func<FColor, float, FColor>) ((x, y) => x * y)},
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
    //----- Required variables
    
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
    
    //----- Required variables with defaults provided
    
    /// <summary>
    /// Easing method used to smooth the tweening process. By default, set to IOSine (not Linear).
    /// </summary>
    public Easer Ease { get; init; } = Easers.EIOSine;
    /// <summary>
    /// Cancellation token used to stop execution.
    /// If set to null, then will use the RunDroppable interface on the executing coroutine manager.
    /// </summary>
    public ICancellee? CToken { get; init; }

    //-----Optional variables set via initializer syntax (or possibly fluent API)
    
    /// <summary>
    /// Lerp function specific to type T. Add handling for types via Tween.RegisterLerper.
    /// </summary>
    private Func<T, T, float, T> Lerp { get; } = Tween.GetLerp<T>();
    /// <summary>
    /// Method to retrieve the delta-time of the current frame. Preferably set this via Tween.DefaultDeltaTimeProvider.
    /// </summary>
    public Func<float> DeltaTimeProvider { get; init; } = DefaultDeltaTimeProvider!;

    /// <summary>
    /// If present, will be executed at task start time to derive the start value.
    /// Overrides Start.
    /// </summary>
    public Func<T>? StartGetter = null;

    /// <summary>
    /// If present, will be executed throughout task execution time to derive the target value.
    /// Overrides End.
    /// </summary>
    public Func<T>? EndGetter = null;
    
    //----- Properties
    private T effectiveStart => StartGetter == null ? Start : StartGetter();
    private T effectiveEnd => EndGetter == null ? End : EndGetter();

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


    private IEnumerator RunIEnum(T start, Action<Completion> done) {
        for (float t = 0; t < Time; t += DeltaTime) {
            if (CToken?.Cancelled == true) 
                break;
            Apply(Lerp(start, effectiveEnd, Ease(t / Time)));
            yield return null;
        }
        if (CToken?.IsHardCancelled() != true)
            Apply(effectiveEnd);
        done(CToken?.ToCompletion() ?? Completion.Standard);
    }

    public async Task<Completion> Run(ICoroutineRunner cors) {
        if (CToken?.IsHardCancelled() == true)
            throw new OperationCanceledException();
        var st = effectiveStart;
        Apply(st);
        var ienum = RunIEnum(st, WaitingUtils.GetCompletionAwaiter(out var t));
        cors.Run(ienum, new CoroutineOptions(CToken == null));
        return await t;
    }

    public ITweener With(ICancellee cT, Func<float> dTProvider) => this with {CToken = cT, DeltaTimeProvider = dTProvider};

    public Tweener<T> Reverse(bool reverseEase = true) => 
        new(End, Start, Time, Apply, reverseEase ? new Easer(t => 1 - Ease(1 - t)) : Ease, CToken) {
            DeltaTimeProvider = DeltaTimeProvider
        };
}

public record SequentialTweener(params ITweener[] states) : ITweener {
    public async Task<Completion> Run(ICoroutineRunner cors) {
        var c = Completion.Standard;
        for (int ii = 0; ii < states.Length; ++ii) {
            try {
                //Report last state
                c = await states[ii].Run(cors);
            } catch (OperationCanceledException) {
                c = Completion.Cancelled;
            }
        }
        if (c == Completion.Cancelled) throw new OperationCanceledException();
        return c;
    }

    public ITweener With(ICancellee cT, Func<float> dTProvider) =>
        new SequentialTweener(states.Select(s => s.With(cT, dTProvider)).ToArray());
}

public record ParallelTweener(params ITweener[] states) : ITweener {

    public async Task<Completion> Run(ICoroutineRunner cors) {
        if (states.Length == 1)
            return await states[0].Run(cors);
        var results = await Task.WhenAll(states.Select(s => s.Run(cors)));
        var c = Completion.Cancelled;
        for (int ii = 0; ii < results.Length; ++ii) {
            if (results[ii] < c)
                c = results[ii];
        }
        if (c == Completion.Cancelled) throw new OperationCanceledException();
        return c;
    }
    
    public ITweener With(ICancellee cT, Func<float> dTProvider) =>
        new ParallelTweener(states.Select(s => s.With(cT, dTProvider)).ToArray());
}

public record LoopTweener(ITweener subj, int? count = null) : ITweener {
    public async Task<Completion> Run(ICoroutineRunner cors) {
        var c = Completion.Standard;
        for (int ii = 0; count == null || ii < count.Value; ++ii) {
            c = await subj.Run(cors);
            if (c > Completion.Standard) {
                if (c == Completion.Cancelled) 
                    throw new OperationCanceledException();
                //If given a fixed iteration number, then run each iteration for softskip.
                if (count == null)
                    return c;
            }
        }
        return c;
    }

    public ITweener With(ICancellee cT, Func<float> dTProvider) =>
        new LoopTweener(subj.With(cT, dTProvider));
}

}