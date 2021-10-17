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
using BagoumLib.Tweening;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Display;
using Suzunoya.Entities;


namespace Suzunoya {
public static class Helpers {
    //Default interface implementations when?

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

    public static VNOperation MakeVNOp(this IEntity e, Func<VNOpTracker, Task> task, bool allowUserSkip=true) {
        return new(e.Container.AssertActive(), task) { AllowUserSkip = allowUserSkip };
    }

    //For these helpers, StartGetter is important in case the tween is run after it is created
    public static VNOperation MoveTo(this ITransform c, Vector3 target, float time, Easer? ease = null) =>
        c.Tween(new Tweener<Vector3>(c.Location.BaseValue, target, time, c.Location.OnNext, ease) {
            StartGetter = () => c.Location.BaseValue
        });

    public static VNOperation MoveBy(this ITransform c, Vector3 delta, float time, Easer? ease = null) =>
        c.Tween(new DeltaTweener<Vector3>(c.Location.BaseValue, delta, time, c.Location.OnNext, ease)
        {
            StartGetter = () => c.Location.BaseValue,
        });

    public static VNOperation RotateTo(this ITransform c, Vector3 targetEulers, float time, Easer? ease = null) =>
        c.Tween(new Tweener<Vector3>(c.EulerAnglesD.BaseValue, targetEulers, time, c.EulerAnglesD.OnNext, ease) {
            StartGetter = () => c.EulerAnglesD.BaseValue
        });
    
    public static VNOperation RotateToClosest(this ITransform c, Vector3 targetEulers, float time, Easer? ease = null) {
        Vector3 src = c.EulerAnglesD.BaseValue;
        var target = new Vector3(
            BMath.GetClosestAroundBound(360f, src.X, targetEulers.X),
            BMath.GetClosestAroundBound(360f, src.Y, targetEulers.Y),
            BMath.GetClosestAroundBound(360f, src.Z, targetEulers.Z));
        return c.Tween(new Tweener<Vector3>(src, target, time, c.EulerAnglesD.OnNext, ease) {
            StartGetter = () => c.EulerAnglesD.BaseValue
        });
    }
        
    public static VNOperation ScaleTo(this ITransform c, Vector3 target, float time, Easer? ease = null) =>
            c.Tween(new Tweener<Vector3>(c.Scale.BaseValue, target, time, c.Scale.OnNext, ease) {
                StartGetter = () => c.Scale.BaseValue
            });

    public static VNOperation FadeTo(this ITinted r, float alpha, float time, Easer? ease = null) =>
        r.Tween(new Tweener<float>(r.Tint.BaseValue.a, alpha, time, a => r.Tint.OnNext(r.Tint.BaseValue.WithA(a)), ease) {
            StartGetter = () => r.Tint.BaseValue.a
        });
    
    public static VNOperation TintTo(this ITinted r, FColor tint, float time, Easer? ease = null) =>
        r.Tween(new Tweener<FColor>(r.Tint.BaseValue, tint, time, r.Tint.OnNext, ease) {
            StartGetter = () => r.Tint.BaseValue
        });

    public static VNOperation ZoomTo(this RenderGroup rg, float zoom, float time, Easer? ease = null) =>
        rg.Tween(new Tweener<float>(rg.Zoom.Value, zoom, time, rg.Zoom.OnNext, ease) {
            StartGetter = () => rg.Zoom.Value
        });

    public static VNOperation AlsoSay(this IDialogueBox dlg, LString content, ICharacter? character = default,
        SpeakFlags flags = SpeakFlags.Default) =>
        dlg.Say(content, character, flags | SpeakFlags.DontClearText);

    public static VNOperation AlsoSayN(this IDialogueBox dlg, LString content, ICharacter? character = default,
        SpeakFlags flags = SpeakFlags.Default) {
        var ncontent = LString.Format("\n{0}", content);
        ncontent.ID = content.ID;
        return AlsoSay(dlg, ncontent, character, flags);
    }


    public static bool TryGetData<T>(this IKeyValueRepository kvr, string? key, out T value) {
        if (kvr.HasData(key)) {
            value = kvr.GetData<T>(key);
            return true;
        }
        value = default!;
        return false;
    }
    
    public static bool SkipsOperations(this SkipMode? sm) => sm switch {
        SkipMode.LOADING => true,
        SkipMode.FASTFORWARD => true,
        _ => false
    };
    public static bool IsPlayerControlled(this SkipMode? sm) => sm switch {
        SkipMode.AUTOPLAY => true,
        SkipMode.FASTFORWARD => true,
        _ => false
    };

    public static Action? SkipGuard(this IVNState vn, Action? act) => 
            act == null ? 
                null :
                () => {
                    if (!vn.SkippingMode.SkipsOperations())
                        act();
                };

    public static VNOperation Disturb<T>(this Entity ent, DisturbedEvented<T> dist, Func<float, T> valuer, 
        float time, bool timeTo01=true) => ent.MakeVNOp(cT => {
        IEnumerator _Disturb(Action done) {
            if (!cT.Cancelled) {
                var ev = new Evented<T>(valuer(0));
                var token = dist.AddDisturbance(ev);
                for (float elapsed = 0; elapsed < time; elapsed += ent.Container.dT) {
                    if (cT.Cancelled)
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