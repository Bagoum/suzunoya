using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using JetBrains.Annotations;
using static BagoumLib.Mathematics.GenericOps;
using static BagoumLib.Transitions.TransitionHelpers;

namespace BagoumLib.Transitions {
/// <summary>
/// Interface for transitions that execute via <see cref="Coroutines"/>.
/// </summary>
[PublicAPI]
public interface ITransition {
    /// <summary>
    /// Runs the transition.
    /// </summary>
    Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null);

    /// <summary>
    /// Nondestructively modify all nested transitions.
    /// </summary>
    ITransition With(ICancellee cT, Func<float> dTProvider);
}

/// <summary>
/// Base class for <see cref="ITransition"/>s over type T.
/// </summary>
/// <typeparam name="T">Type of the value that is being modified</typeparam>
public abstract record TransitionBase<T> : ITransition {
    /// <summary>
    /// Amount of time over which to perform tweening.
    /// </summary>
    public float Time { get; init; }
    
    /// <summary>
    /// Cancellation token used to stop execution.
    /// If set to null, then will use the RunDroppable interface on the executing coroutine manager.
    /// </summary>
    public ICancellee? CToken { get; init; }
    
    /// <summary>
    /// Method to retrieve the delta-time of the current frame. Preferably set this via Tween.DefaultDeltaTimeProvider.
    /// </summary>
    public Func<float> DeltaTimeProvider { get; init; } = DefaultDeltaTimeProvider!;

    /// <summary>
    /// Method to apply an updated tweened value. (Required during initialization.)
    /// </summary>
    public Action<T> Apply { get; init; } = null!;
    
    /// <summary>
    /// Easing method used to smooth the tweening process. By default, set to IOSine (not Linear).
    /// </summary>
    public Easer Ease { get; init; } = Easers.EIOSine;
    
    private float DeltaTime => (DeltaTimeProvider ?? throw new Exception(
        "No delta time provider has been set! It is recommended to set TweenHelpers.DefaultDeltaTimeProvider."))();

    /// <summary>
    /// Apply the initial value of the transition.
    /// </summary>
    /// <returns></returns>
    protected abstract T ApplyStart();
    
    /// <summary>
    /// Apply the value of the transition at a given time.
    /// </summary>
    protected abstract void ApplyStep(T start, float time);
    
    /// <summary>
    /// Apply the final value of the transition.
    /// </summary>
    protected abstract void ApplyEnd(T start);
    
    private IEnumerator RunIEnum(T start, Action<Completion> done) {
        for (float t = 0; t < Time; t += DeltaTime) {
            if (CToken?.Cancelled == true) 
                break;
            ApplyStep(start, t);
            yield return null;
        }
        if (CToken?.IsHardCancelled() != true)
            ApplyEnd(start);
        done(CToken?.ToCompletion() ?? Completion.Standard);
    }

    /// <inheritdoc/>
    public async Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null) {
        if (CToken?.IsHardCancelled() == true)
            throw new OperationCanceledException();
        var ienum = RunIEnum(ApplyStart(), WaitingUtils.GetCompletionAwaiter(out var t));
        cors.Run(ienum, options ?? CoroutineOptions.DefaultWith(CToken == null));
        return await t;
    }

    /// <inheritdoc/>
    public ITransition With(ICancellee cT, Func<float> dTProvider) => this with {CToken = cT, DeltaTimeProvider = dTProvider};
}

/// <summary>
/// Helpers for creating transitions.
/// </summary>
[PublicAPI]
public static class TransitionHelpers {
    /// <summary>
    /// Default provider for transition deltaTime. Should be provided by consuming libraries.
    /// </summary>
    public static Func<float>? DefaultDeltaTimeProvider { get; set; }

    /// <summary>
    /// Create a transition that applies a value provided by `eval` at every time step.
    /// </summary>
    public static StatusApplier<T> Apply<T>(Func<float, T> eval, float time, Action<T> apply, Func<T>? initVal = null, 
        ICancellee? cT = null) =>
        new(eval, time, apply, cT) { InitialValue = initVal };
    
    /// <summary>
    /// Create a transition that lerps from `start` to `end`.
    /// </summary>
    public static Tweener<T> TweenTo<T>(T start, T end, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        new(start, end, time, apply, ease, cT);
    
    /// <summary>
    /// Create a transition that lerps from `start` to `start+delta`.
    /// </summary>
    public static DeltaTweener<T> TweenDelta<T>(T start, T delta, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        new(start, delta, time, apply, ease, cT);

    /// <summary>
    /// Create a transition that lerps from `start` to `start*by`.
    /// </summary>
    public static Tweener<T> TweenBy<T>(T start, float by, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        TweenTo(start, GetMulOp<T>()(start, by), time, apply, ease, cT);

    /// <summary>
    /// Run two transitions in sequence.
    /// </summary>
    public static ITransition Then(this ITransition tw, ITransition next) => new SequentialTransition(new(tw), new(next));
    
    /// <inheritdoc cref="Then(ITransition,ITransition)"/>
    public static ITransition Then(this ITransition tw, Func<ITransition> next) => new SequentialTransition(new(tw), next);
    
    /// <summary>
    /// Run multiple transitions in parallel.
    /// </summary>
    public static ITransition Parallel(params ITransition[] tws) => new ParallelTransition(tws);
    
    /// <summary>
    /// Run multiple transitions in parallel.
    /// </summary>
    public static ITransition Parallel(this ITransition tw, params ITransition[] tws) => new ParallelTransition(tws.Prepend(tw).ToArray());
    
    /// <summary>
    /// Loop a transition.
    /// </summary>
    public static ITransition Loop(this ITransition tw, int? times = null) => new LoopTransition(tw, times);
}
}