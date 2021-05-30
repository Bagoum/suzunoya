using System;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
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

    public static VNOperation MakeVNOp(this IEntity e, Func<ICancellee, Task> task) {
        return new(e.Container.AssertActive(), null, task);
    }

    //For these helpers, StartGetter is important in case the tween is run after it is created
    public static VNOperation MoveTo(this ITransform c, Vector3 target, float time, Easer? ease = null) =>
        c.Tween(new Tweener<Vector3>(c.Location, target, time, c.Location.OnNext, ease) {
            StartGetter = () => c.Location
        });

    public static VNOperation RotateTo(this ITransform c, Vector3 targetEulers, float time, Easer? ease = null) {
        Vector3 src = c.EulerAnglesD;
        var target = new Vector3(
            BMath.GetClosestAroundBound(360f, src.X, targetEulers.X),
            BMath.GetClosestAroundBound(360f, src.Y, targetEulers.Y),
            BMath.GetClosestAroundBound(360f, src.Z, targetEulers.Z));
        return c.Tween(new Tweener<Vector3>(src, target, time, c.EulerAnglesD.OnNext, ease) {
            StartGetter = () => c.EulerAnglesD
        });
    }
        
    public static VNOperation ScaleTo(this ITransform c, Vector3 target, float time, Easer? ease = null) =>
            c.Tween(new Tweener<Vector3>(c.Scale, target, time, c.Scale.OnNext, ease) {
                StartGetter = () => c.Scale
            });

    public static VNOperation FadeTo(this IRendered r, float alpha, float time, Easer? ease = null) =>
        r.Tween(new Tweener<float>(r.Tint.Value.a, alpha, time, a => r.Tint.OnNext(r.Tint.Value.WithA(a)), ease) {
            StartGetter = () => r.Tint.Value.a
        });
    
    public static VNOperation TintTo(this IRendered r, FColor tint, float time, Easer? ease = null) =>
        r.Tween(new Tweener<FColor>(r.Tint.Value, tint, time, r.Tint.OnNext, ease) {
            StartGetter = () => r.Tint.Value
        });

    public static VNOperation ZoomTo(this RenderGroup rg, float zoom, float time, Easer? ease = null) =>
        rg.Tween(new Tweener<float>(rg.Zoom.Value, zoom, time, rg.Zoom.OnNext, ease) {
            StartGetter = () => rg.Zoom.Value
        });

    /*
    public static Task SayC(this IDialogueBox dlg, string content, ICharacter? character = default,
        SpeakFlags flags = SpeakFlags.Default) => 
        dlg.Say(content, character, flags | SpeakFlags.WaitConfirm);*/

    public static VNOperation AlsoSay(this IDialogueBox dlg, string content, ICharacter? character = default,
        SpeakFlags flags = SpeakFlags.Default) =>
        dlg.Say(content, character, flags | SpeakFlags.DontClearText);

    public static VNOperation AlsoSayN(this IDialogueBox dlg, string content, ICharacter? character = default,
        SpeakFlags flags = SpeakFlags.Default) => AlsoSay(dlg, "\n" + content, character, flags);
    
    
    public static bool TryGetData<T>(this IKeyValueRepository kvr, string? key, out T value) {
        if (kvr.HasData(key)) {
            value = kvr.GetData<T>(key);
            return true;
        }
        value = default!;
        return false;
    }
}
}