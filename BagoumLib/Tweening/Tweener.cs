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
using static BagoumLib.Mathematics.GenericOps;
using static BagoumLib.Tweening.Tween;

namespace BagoumLib.Tweening {
[PublicAPI]
public interface ITweener {
    /// <summary>
    /// Runs the tweener.
    /// </summary>
    Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null);

    /// <summary>
    /// Nondestructively modify all nested tweeners.
    /// </summary>
    ITweener With(ICancellee cT, Func<float> dTProvider);
}

[PublicAPI]
public static class Tween {
    public static Func<float>? DefaultDeltaTimeProvider { get; set; }

    public static Tweener<T> TweenTo<T>(T start, T end, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        new(start, end, time, apply, ease, cT);
    
    public static DeltaTweener<T> TweenDelta<T>(T start, T delta, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        new(start, delta, time, apply, ease, cT);

    public static Tweener<T> TweenBy<T>(T start, float by, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        TweenTo(start, GetMulOp<T>()(start, by), time, apply, ease, cT);

    public static ITweener Then(this ITweener tw, ITweener next) => new SequentialTweener(tw, next);
    public static ITweener Parallel(params ITweener[] tws) => new ParallelTweener(tws);
    public static ITweener Loop(this ITweener tw, int? times = null) => new LoopTweener(tw, times);
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
    /// Lerp function specific to type T (unclamped). Add handling for types via Tween.RegisterLerper.
    /// </summary>
    private Func<T, T, float, T> Lerp { get; } = GetLerp<T>();
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

    public async Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null) {
        if (CToken?.IsHardCancelled() == true)
            throw new OperationCanceledException();
        var st = effectiveStart;
        Apply(st);
        var ienum = RunIEnum(st, WaitingUtils.GetCompletionAwaiter(out var t));
        cors.Run(ienum, options ?? new CoroutineOptions(CToken == null));
        return await t;
    }

    public ITweener With(ICancellee cT, Func<float> dTProvider) => this with {CToken = cT, DeltaTimeProvider = dTProvider};

    /// <summary>
    /// TODO a delayed invocation uses startgetter, and should pass that to Reverse, but Reverse won't work with the same startgetter.
    /// TODO Reverse is probably better defined with a "delta" field instead.
    /// </summary>
    public Tweener<T> Reverse(bool reverseEase = true) => 
        new(End, Start, Time, Apply, reverseEase ? new Easer(t => 1 - Ease(1 - t)) : Ease, CToken) {
            DeltaTimeProvider = DeltaTimeProvider
        };

    public ITweener Yoyo(bool reverseEase = true, int? times = null) => this.Then(this.Reverse(reverseEase)).Loop(times);
}

[PublicAPI]
public record DeltaTweener<T> : ITweener {
    //----- Required variables
    
    /// <summary>
    /// Initial value of tweening.
    /// </summary>
    public T Start { get; init; }
    /// <summary>
    /// Delta to apply to initial value.
    /// </summary>
    public T Delta { get; init; }
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
    private Func<T, T, float, T> Lerp { get; } = GetLerp<T>();
    private Func<T, T, T> Add { get; } = GetAddOp<T>();
    /// <summary>
    /// Method to retrieve the delta-time of the current frame. Preferably set this via Tween.DefaultDeltaTimeProvider.
    /// </summary>
    public Func<float> DeltaTimeProvider { get; init; } = DefaultDeltaTimeProvider!;
    
    /// <summary>
    /// If present, will be executed at task start time to derive the start value.
    /// Overrides Start.
    /// </summary>
    public Func<T>? StartGetter = null;
    
    //----- Properties
    private T effectiveStart => StartGetter == null ? Start : StartGetter();
    
    public DeltaTweener(T start, T delta, float time, Action<T> apply, Easer? ease = null, ICancellee? cT = null) {
        Start = start;
        Delta = delta;
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
            Apply(Lerp(start, Add(start, Delta), Ease(t / Time)));
            yield return null;
        }
        if (CToken?.IsHardCancelled() != true)
            Apply(Add(start, Delta));
        done(CToken?.ToCompletion() ?? Completion.Standard);
    }
    
    public async Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null) {
        if (CToken?.IsHardCancelled() == true)
            throw new OperationCanceledException();
        var st = effectiveStart;
        Apply(st);
        var ienum = RunIEnum(st, WaitingUtils.GetCompletionAwaiter(out var t));
        cors.Run(ienum, options ?? new CoroutineOptions(CToken == null));
        return await t;
    }
    
    public ITweener With(ICancellee cT, Func<float> dTProvider) => this with {CToken = cT, DeltaTimeProvider = dTProvider};
    
}

public record SequentialTweener(params ITweener[] states) : ITweener {
    public async Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null) {
        var c = Completion.Standard;
        for (int ii = 0; ii < states.Length; ++ii) {
            try {
                //Report last state
                c = await states[ii].Run(cors, options);
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

    public async Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null) {
        if (states.Length == 1)
            return await states[0].Run(cors, options);
        var results = await Task.WhenAll(states.Select(s => s.Run(cors, options)));
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
    public async Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null) {
        var c = Completion.Standard;
        for (int ii = 0; count == null || ii < count.Value; ++ii) {
            c = await subj.Run(cors, options);
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