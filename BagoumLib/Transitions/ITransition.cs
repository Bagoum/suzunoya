using System;
using System.Collections;
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
    private float DeltaTime => (DeltaTimeProvider ?? throw new Exception(
        "No delta time provider has been set! It is recommended to set TweenHelpers.DefaultDeltaTimeProvider."))();



    protected abstract T ApplyStart();
    protected abstract void ApplyStep(T start, float time);
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

    public async Task<Completion> Run(ICoroutineRunner cors, CoroutineOptions? options = null) {
        if (CToken?.IsHardCancelled() == true)
            throw new OperationCanceledException();
        var ienum = RunIEnum(ApplyStart(), WaitingUtils.GetCompletionAwaiter(out var t));
        cors.Run(ienum, options ?? new CoroutineOptions(CToken == null));
        return await t;
    }

    public ITransition With(ICancellee cT, Func<float> dTProvider) => this with {CToken = cT, DeltaTimeProvider = dTProvider};
}


[PublicAPI]
public static class TransitionHelpers {
    public static Func<float>? DefaultDeltaTimeProvider { get; set; }

    public static StatusApplier<T> Apply<T>(Func<float, T> eval, float time, Action<T> apply, Func<T>? initVal = null, 
        ICancellee? cT = null) =>
        new(eval, time, apply, cT) { InitialValue = initVal };
    public static Tweener<T> TweenTo<T>(T start, T end, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        new(start, end, time, apply, ease, cT);
    
    public static DeltaTweener<T> TweenDelta<T>(T start, T delta, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        new(start, delta, time, apply, ease, cT);

    public static Tweener<T> TweenBy<T>(T start, float by, float time, Action<T> apply, Easer? ease = null, 
        ICancellee? cT = null) =>
        TweenTo(start, GetMulOp<T>()(start, by), time, apply, ease, cT);

    public static ITransition Then(this ITransition tw, ITransition next) => new SequentialTransition(() => tw, () => next);
    public static ITransition Then(this ITransition tw, Func<ITransition> next) => new SequentialTransition(() => tw, next);
    public static ITransition Parallel(params ITransition[] tws) => new ParallelTransition(tws);
    public static ITransition Loop(this ITransition tw, int? times = null) => new LoopTransition(tw, times);
}
}