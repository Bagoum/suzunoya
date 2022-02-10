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
using static BagoumLib.Transitions.TransitionHelpers;

namespace BagoumLib.Transitions {
[PublicAPI]
public record Tweener<T> : TransitionBase<T> {
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
    /// Method to update tweened value.
    /// </summary>
    public Action<T> Apply { get; init; }
    
    //----- Required variables with defaults provided
    
    /// <summary>
    /// Easing method used to smooth the tweening process. By default, set to IOSine (not Linear).
    /// </summary>
    public Easer Ease { get; init; } = Easers.EIOSine;

    //-----Optional variables set via initializer syntax (or possibly fluent API)
    
    /// <summary>
    /// Lerp function specific to type T (unclamped). Add handling for types via Tween.RegisterLerper.
    /// </summary>
    private Func<T, T, float, T> Lerp { get; } = GetLerp<T>();

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

    protected override T ApplyStart() {
        var st = effectiveStart;
        Apply(st);
        return st;
    }

    protected override void ApplyStep(T start, float time) => Apply(Lerp(start, effectiveEnd, Ease(time / Time)));
    protected override void ApplyEnd(T _) => Apply(effectiveEnd);

    /// <summary>
    /// TODO a delayed invocation uses startgetter, and should pass that to Reverse, but Reverse won't work with the same startgetter.
    /// TODO Reverse is probably better defined with a "delta" field instead.
    /// </summary>
    public Tweener<T> Reverse(bool reverseEase = true) => 
        new(End, Start, Time, Apply, reverseEase ? new Easer(t => 1 - Ease(1 - t)) : Ease, CToken) {
            DeltaTimeProvider = DeltaTimeProvider
        };

    public ITransition Yoyo(bool reverseEase = true, int? times = null) => this.Then(this.Reverse(reverseEase)).Loop(times);
}

[PublicAPI]
public record DeltaTweener<T> : TransitionBase<T> {
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
    /// Method to update tweened value.
    /// </summary>
    public Action<T> Apply { get; init; }
    
    //----- Required variables with defaults provided
    
    /// <summary>
    /// Easing method used to smooth the tweening process. By default, set to IOSine (not Linear).
    /// </summary>
    public Easer Ease { get; init; } = Easers.EIOSine;
    
    //-----Optional variables set via initializer syntax (or possibly fluent API)
    
    /// <summary>
    /// Lerp function specific to type T. Add handling for types via Tween.RegisterLerper.
    /// </summary>
    private static readonly Func<T, T, float, T> Lerp = GetLerp<T>();
    private static readonly Func<T, T, T> Add = GetAddOp<T>().add;
    
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
    
    protected override T ApplyStart() {
        var st = effectiveStart;
        Apply(st);
        return st;
    }

    protected override void ApplyStep(T start, float time) => Apply(Lerp(start, Add(start, Delta), Ease(time / Time)));
    protected override void ApplyEnd(T start) => Apply(Add(start, Delta));
    
}

public record SequentialTransition(params Func<ITransition>[] states) : ITransition {
    public async Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null) {
        var c = Completion.Standard;
        for (int ii = 0; ii < states.Length; ++ii) {
            try {
                //Report last state
                c = await states[ii]().Run(cors, options);
            } catch (OperationCanceledException) {
                c = Completion.Cancelled;
            }
        }
        if (c == Completion.Cancelled) throw new OperationCanceledException();
        return c;
    }

    public ITransition With(ICancellee cT, Func<float> dTProvider) =>
        new SequentialTransition(states.Select<Func<ITransition>, Func<ITransition>>(s => () => s().With(cT, dTProvider)).ToArray());
}

public record ParallelTransition(params ITransition[] states) : ITransition {

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
    
    public ITransition With(ICancellee cT, Func<float> dTProvider) =>
        new ParallelTransition(states.Select(s => s.With(cT, dTProvider)).ToArray());
}

public record LoopTransition(ITransition subj, int? count = null) : ITransition {
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

    public ITransition With(ICancellee cT, Func<float> dTProvider) =>
        new LoopTransition(subj.With(cT, dTProvider));
}

}