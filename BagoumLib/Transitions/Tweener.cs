using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using JetBrains.Annotations;
using static BagoumLib.Mathematics.GenericOps;
using static BagoumLib.Transitions.TransitionHelpers;

namespace BagoumLib.Transitions {
/// <summary>
/// A transition that lerps a value from <see cref="Start"/> to <see cref="End"/>.
/// </summary>
[PublicAPI]
public record Tweener<T> : TransitionBase<T> {
    //----- Required variables
    
    /// <summary>
    /// Initial value of tweening.
    /// </summary>
    public Either<T, Func<T>> Start { get; init; }
    /// <summary>
    /// Final value of tweening.
    /// </summary>
    public Either<T, Func<T>> End { get; init; }

    //-----Optional variables set via initializer syntax (or possibly fluent API)
    
    /// <summary>
    /// Lerp function specific to type T (unclamped). Add handling for types via Tween.RegisterLerper.
    /// </summary>
    private Func<T, T, float, T> Lerp { get; } = GetLerp<T>();


    public Tweener(Either<T, Func<T>> start, Either<T, Func<T>> end, float time, Action<T> apply, Easer? ease = null, ICancellee? cT = null) {
        Start = start;
        End = end;
        Time = time;
        Apply = apply;
        CToken = cT;
        Ease = ease ?? Ease;
    }

    /// <inheritdoc/>
    protected override T ApplyStart() {
        var st = Start.Resolve();
        Apply(st);
        return st;
    }

    /// <inheritdoc/>
    protected override void ApplyStep(T start, float time) => Apply(Lerp(start, End.Resolve(), Ease(time / Time)));
    
    /// <inheritdoc/>
    protected override void ApplyEnd(T _) => Apply(End.Resolve());

    /// <summary>
    /// TODO a delayed invocation uses startgetter, and should pass that to Reverse, but Reverse won't work with the same startgetter.
    /// TODO Reverse is probably better defined with a "delta" field instead.
    /// </summary>
    public Tweener<T> Reverse(bool reverseEase = true) => 
        new(End, Start, Time, Apply, reverseEase ? new Easer(t => 1 - Ease(1 - t)) : Ease, CToken) {
            DeltaTimeProvider = DeltaTimeProvider
        };

    /// <summary>
    /// Create a yoyo transition from this transition; ie. a transition that runs this, runs this in reverse, and then repeats.
    /// </summary>
    /// <param name="reverseEase">True if the easing function should be reversed for the reverse section.</param>
    /// <param name="times">Number of times to repeat the joined transition; null for indefinite.</param>
    public ITransition Yoyo(bool reverseEase = true, int? times = null) => this.Then(this.Reverse(reverseEase)).Loop(times);
}

/// <summary>
/// A tweener that does nothing for the provided amount of time.
/// </summary>
public record NoopTweener : TransitionBase<Unit> {
    public NoopTweener(float time, ICancellee? cT = null) {
        Time = time;
        CToken = cT;
    }
    
    /// <inheritdoc/>
    protected override Unit ApplyStart() => Unit.Default;

    /// <inheritdoc/>
    protected override void ApplyStep(Unit start, float time) { }

    /// <inheritdoc/>
    protected override void ApplyEnd(Unit start) { }
}

/// <summary>
/// A transition that scales in a value using scalar multiplication.
/// </summary>
public record ScaleInTweener<T> : TransitionBase<T> {
    /// <summary>
    /// The value to be scaled in by the tweener.
    /// </summary>
    public T Target { get; init; }

    //-----Optional variables set via initializer syntax (or possibly fluent API)
    
    /// <summary>
    /// Multiplier function specific to type T. Add handling for types via Tween.RegisterMultiplier.
    /// </summary>
    private Func<T, float, T> Scaler { get; } = GetMulOp<T>();

    public ScaleInTweener(T target, float time, Action<T> apply, Easer? ease = null, ICancellee? cT = null) {
        Target = target;
        Time = time;
        Apply = apply;
        CToken = cT;
        Ease = ease ?? Ease;
    }

    /// <inheritdoc/>
    protected override T ApplyStart() {
        var st = Scaler(Target, 0);
        Apply(st);
        return st;
    }

    /// <inheritdoc/>
    protected override void ApplyStep(T _, float time) => Apply(Scaler(Target, Ease(time / Time)));
    
    /// <inheritdoc/>
    protected override void ApplyEnd(T _) => Apply(Target);
}

/// <summary>
/// A transition that gradually adds a delta to a value.
/// </summary>
[PublicAPI]
public record DeltaTweener<T> : TransitionBase<T> {
    //----- Required variables
    
    /// <summary>
    /// Initial value of tweening.
    /// </summary>
    public Either<T, Func<T>> Start { get; init; }
    /// <summary>
    /// Delta to apply to initial value.
    /// </summary>
    public T Delta { get; init; }

    //-----Optional variables set via initializer syntax (or possibly fluent API)
    
    /// <summary>
    /// Lerp function specific to type T. Add handling for types via Tween.RegisterLerper.
    /// </summary>
    private static readonly Func<T, T, float, T> Lerp = GetLerp<T>();
    private static readonly Func<T, T, T> Add = GetAddOp<T>().add;
    
    public DeltaTweener(Either<T, Func<T>> start, T delta, float time, Action<T> apply, Easer? ease = null, ICancellee? cT = null) {
        Start = start;
        Delta = delta;
        Time = time;
        Apply = apply;
        CToken = cT;
        Ease = ease ?? Ease;
    }
    
    /// <inheritdoc/>
    protected override T ApplyStart() {
        var st = Start.Resolve();
        Apply(st);
        return st;
    }

    /// <inheritdoc/>
    protected override void ApplyStep(T start, float time) => Apply(Lerp(start, Add(start, Delta), Ease(time / Time)));
    
    /// <inheritdoc/>
    protected override void ApplyEnd(T start) => Apply(Add(start, Delta));
    
}

/// <summary>
/// A transition that runs multiple transitions in sequence.
/// </summary>
public record SequentialTransition(params Delayed<ITransition>[] states) : ITransition {
    /// <inheritdoc/>
    public async Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null) {
        var c = Completion.Standard;
        for (int ii = 0; ii < states.Length; ++ii) {
            try {
                //Report last state
                c = await states[ii].Value.Run(cors, options);
            } catch (OperationCanceledException) {
                c = Completion.Cancelled;
            }
        }
        if (c == Completion.Cancelled) throw new OperationCanceledException();
        return c;
    }

    /// <inheritdoc/>
    public ITransition With(ICancellee cT, Func<float> dTProvider) =>
        new SequentialTransition(states.Select(s => s.FMap(tr => tr.With(cT, dTProvider))).ToArray());
}


/// <summary>
/// A transition that runs multiple transitions in parallel.
/// </summary>
public record ParallelTransition(params ITransition[] states) : ITransition {
    /// <inheritdoc/>
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
    
    /// <inheritdoc/>
    public ITransition With(ICancellee cT, Func<float> dTProvider) =>
        new ParallelTransition(states.Select(s => s.With(cT, dTProvider)).ToArray());
}


/// <summary>
/// A transition that loops a child transition.
/// </summary>
public record LoopTransition(ITransition subj, int? count = null) : ITransition {
    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public ITransition With(ICancellee cT, Func<float> dTProvider) =>
        new LoopTransition(subj.With(cT, dTProvider));
}

}