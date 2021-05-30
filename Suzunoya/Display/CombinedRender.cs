using System;
using System.Collections;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Tasks;

namespace Suzunoya.Display {
//this is a plugin class with functionality parallel to BackgroundCombiner.
//Based on this implementation, we can see that RenderGroup has effectively zero code in terms of transitions/combined renderings.
/*
public class RGDisplayer {
    private ArbitraryCapturer capturer;
    private readonly RenderGroup rg;
    private MaterialPropertyBlock pb;
    private Material mat;

    public Task DoFadeTransition(RenderGroup target, float time) {
        //this is the most straightforward way to get access to the plugin-side RenderTexture of the other RG
        var target_d = RGDisplayer.Find(target);
        var cT = rg.GetTransitionToken();
        IEnumerator Inner(Action done) {
            Assert(rg.Visible == true);
            Assert(target.Visible == false);
            cT.ThrowIfHardCancelled();
            pb.EnableKeyword(COMPUTED);
            for (float t = 0; t < time; t += 0.05f) {
                if (cT.Cancelled) break;
                pb.SetTexture(PropConsts.target, target_d.capturer.capturedTexture);
                pb.SetFloat(PropConsts.ratio, t / time);
                yield return null;
                cT.ThrowIfHardCancelled();
            }
            rg.Visible.Value = false;
            target.Visible.Value = true;
            pb.DisableKeyword(COMPUTED);
        }
        rg.Run(Inner(WaitingUtils.GetAwaiter(out Task t)));
        return t;
    }

}
*/

}