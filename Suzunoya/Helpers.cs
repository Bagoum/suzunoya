using System;
using System.Collections;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using BagoumLib.Transitions;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Display;
using Suzunoya.Entities;


namespace Suzunoya {
/// <summary>
/// Various helper extensions for Suzunoya code.
/// </summary>
[PublicAPI]
public static class Helpers {
    /// <summary>
    /// Check that a VN is active before returning it. If it is inactive, throw <see cref="DestroyedObjectException"/>.
    /// </summary>
    public static T AssertActive<T>(this T vn) where T : IVNState {
        if (vn.VNStateActive.Value == false)
            throw new DestroyedObjectException("The VNState has been destroyed. " +
                                               "You cannot run any more operations on it.");
        return vn;
    }

    /// <summary>
    /// Makes a derived cancellee that is cancelled by both the entity lifetime token and the given cT.
    /// <br/>Coroutines run on entities should pass their given tokens through this function.
    /// </summary>
    public static ICancellee BindLifetime(this IEntity e, ICancellee cT) => new JointCancellee(e.LifetimeToken, cT);

    /// <summary>
    /// Construct a <see cref="VNOperation"/> from a task running on an entity.
    /// </summary>
    /// <param name="e">Entity on which a task is run.</param>
    /// <param name="task">Task to be run.</param>
    /// <param name="allowUserSkip">True iff the task can be soft-skipped by user input (<see cref="VNOperation.AllowUserSkip"/>)</param>
    /// <returns></returns>
    public static VNOperation MakeVNOp(this IEntity e, Func<VNCancellee, Task> task, bool allowUserSkip=true) {
        return new(e.Container.AssertActive(), task) { AllowUserSkip = allowUserSkip };
    }

    //For these helpers, using the delayed value is important in case the tween is run after it is created
    /// <summary>
    /// Create a tweener for moving an entity to a target position.
    /// </summary>
    public static VNOperation MoveTo(this ITransform c, Vector3 target, float time, Easer? ease = null) =>
        c.Tween(new Tweener<Vector3>(new(() => c.LocalLocation.Value), target, time, c.LocalLocation.OnNext, ease));

    /// <summary>
    /// Create a tweener for moving an entity by a delta.
    /// </summary>
    public static VNOperation MoveBy(this ITransform c, Vector3 delta, float time, Easer? ease = null) =>
        c.Tween(new DeltaTweener<Vector3>(new(() => c.LocalLocation.Value), delta, time, c.LocalLocation.OnNext, ease));

    /// <summary>
    /// Create a tweener for rotating an entity to a target eulers.
    /// </summary>
    public static VNOperation RotateTo(this ITransform c, Vector3 targetEulers, float time, Easer? ease = null) =>
        c.Tween(new Tweener<Vector3>(new(() => c.EulerAnglesD.Value), targetEulers, 
            time, c.EulerAnglesD.OnNext, ease));
    
    /// <summary>
    /// Create a tweener for rotating an entity to a target eulers, ignoring any multiples of 360 and moving in the closest direction.
    /// </summary>
    public static VNOperation RotateToClosest(this ITransform c, Vector3 targetEulers, float time, Easer? ease = null) =>
        c.Tween(new Tweener<Vector3>(new(() => c.EulerAnglesD.Value), new(() => {
            var src = c.EulerAnglesD.Value;
            return new Vector3(
                BMath.GetClosestAroundBound(360f, src.X, targetEulers.X),
                BMath.GetClosestAroundBound(360f, src.Y, targetEulers.Y),
                BMath.GetClosestAroundBound(360f, src.Z, targetEulers.Z));
        }), time, c.EulerAnglesD.OnNext, ease));
    
    
    /// <summary>
    /// Create a tweener for rotating an entity to a target eulers, ignoring any multiples of 360 and moving in the closest direction. Uses quaternions internally.
    /// </summary>
    public static VNOperation RotateToClosestQ(this ITransform c, Vector3 targetEulers, float time, Easer? ease = null) =>
        c.Tween(new Tweener<Quaternion>(new(() => c.EulerAnglesD.Value.ToQuaternionD()), new(() => {
            var src = c.EulerAnglesD.Value;
            return new Vector3(
                BMath.GetClosestAroundBound(360f, src.X, targetEulers.X),
                BMath.GetClosestAroundBound(360f, src.Y, targetEulers.Y),
                BMath.GetClosestAroundBound(360f, src.Z, targetEulers.Z)).ToQuaternionD();
        }), time, q => c.EulerAnglesD.OnNext(q.ToEulersD()), ease));

    /// <summary>
    /// Create a tweener for scaling an entity up to a target.
    /// </summary>
    public static VNOperation ScaleTo(this ITransform c, Vector3 target, float time, Easer? ease = null) =>
            c.Tween(new Tweener<Vector3>(new(() => c.Scale.Value), target, time, c.Scale.OnNext, ease));

    /// <summary>
    /// Create a tweener for the alpha value of an entity's tint.
    /// </summary>
    public static VNOperation FadeTo(this ITinted r, float alpha, float time, Easer? ease = null) =>
        r.Tween(new Tweener<float>(new(() => r.Tint.Value.a), alpha, time, a => r.Tint.OnNext(r.Tint.Value.WithA(a)), ease));
    
    /// <summary>
    /// Create a tweener for changing the tint of an entity.
    /// </summary>
    public static VNOperation TintTo(this ITinted r, FColor tint, float time, Easer? ease = null) =>
        r.Tween(new Tweener<FColor>(new(() => r.Tint.Value), tint, time, r.Tint.OnNext, ease));

    /// <summary>
    /// Create a tweener for changing the zoom of a render group.
    /// </summary>
    public static VNOperation ZoomTo(this RenderGroup rg, float zoom, float time, Easer? ease = null) =>
        rg.Tween(new Tweener<float>(new(() => rg.Zoom), zoom, time, rg.Zoom.OnNext, ease));

    /// <summary>
    /// <see cref="IDialogueBox.Say"/> with <see cref="SpeakFlags.DontClearText"/>.
    /// </summary>
    public static VNOperation AlsoSay(this IDialogueBox dlg, LString content, ICharacter? character = default,
        SpeakFlags flags = SpeakFlags.Default) =>
        dlg.Say(content, character, flags | SpeakFlags.DontClearText);

    /// <summary>
    /// <see cref="IDialogueBox.Say"/> with <see cref="SpeakFlags.DontClearText"/> and a newline at the beginning.
    /// </summary>
    public static VNOperation AlsoSayN(this IDialogueBox dlg, LString content, ICharacter? character = default,
        SpeakFlags flags = SpeakFlags.Default) {
        var ncontent = LString.Format("\n{0}", content);
        ncontent.ID = content.ID;
        return AlsoSay(dlg, ncontent, character, flags);
    }

    /// <summary>
    /// Run <see cref="IKeyValueRepository.GetData{T}"/> if a key exists in the repository.
    /// </summary>
    public static bool TryGetData<T>(this IKeyValueRepository kvr, string? key, out T value) {
        if (kvr.HasData(key)) {
            value = kvr.GetData<T>(key);
            return true;
        }
        value = default!;
        return false;
    }
    
    /// <summary>
    /// Returns true if the mode automatically skips lines (loading or fastforward).
    /// </summary>
    public static bool SkipsOperations(this SkipMode? sm) => sm switch {
        SkipMode.LOADING => true,
        SkipMode.FASTFORWARD => true,
        _ => false
    };
    
    /// <summary>
    /// Returns true if the skip mode can be changed by the player (autoplay or fastforward).
    /// </summary>
    public static bool IsPlayerControlled(this SkipMode? sm) => sm switch {
        SkipMode.AUTOPLAY => true,
        SkipMode.FASTFORWARD => true,
        _ => false
    };

    /// <summary>
    /// Run an action only if the skip mode does not automatically skip lines.
    /// </summary>
    public static Action? SkipGuard(this IVNState vn, Action? act) => 
            act == null ? 
                null :
                () => {
                    if (!vn.SkippingMode.SkipsOperations())
                        act();
                };

    /// <summary>
    /// Add a disturbance effect over time to a <see cref="IDisturbable{T}"/>.
    /// </summary>
    /// <param name="ent">Entity on which the coroutine for the disturbance effect should be run.</param>
    /// <param name="dist">Base value to which to add the effect.</param>
    /// <param name="valuer">Function that produces the value of the disturbance.</param>
    /// <param name="time">Amount of time for which to run the disturbance effect.</param>
    /// <param name="timeTo01">True iff `valuer` operates over the range [0, 1], false iff over [0, time].</param>
    public static VNOperation Disturb<T>(this Entity ent, IDisturbable<T> dist, Func<float, T> valuer, 
        float time, bool timeTo01=true) => ent.MakeVNOp(cT => {
        IEnumerator _Disturb(Action done) {
            if (cT.CancelLevel == 0) {
                var ev = new Evented<T>(valuer(0));
                var token = dist.AddDisturbance(ev);
                for (float elapsed = 0; elapsed < time; elapsed += ent.Container.dT) {
                    if (cT.CancelLevel > 0)
                        break;
                    ev.OnNext(valuer(timeTo01 ? (elapsed / time) : elapsed));
                    yield return null;
                }
                token.Dispose();
            }
            done();
        }
        ent.Run(_Disturb(WaitingUtils.GetAwaiter(out Task t)));
        return t;
    });
}
}