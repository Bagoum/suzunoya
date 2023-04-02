using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using NUnit.Framework;
using Suzunoya.Dialogue;
using Suzunoya.Display;
using Suzunoya.Entities;
using static Tests.Suzunoya.MyTestCharacter;
using static Tests.Suzunoya.ScriptTestHelpers;
using static Tests.AssertHelpers;
using static Suzunoya.Helpers;

namespace Tests.Suzunoya {

public class _8RenderGroupTest {
    private class _TestScript : TestScript {
        public async Task<int> Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            await vn.Wait(0);
            
            var rg2 = vn.Add(new RenderGroup("rg2", 2));
            var rg3 = vn.Add(new RenderGroup("rg3", 3));
            //Multiple render groups cannot have the same priority (at least not on initialization)
            Assert.Throws<Exception>(() => vn.Add(new RenderGroup("rg4", 2)));
            
            reimu.AddToRenderGroup(rg2);
            ListEq(rg2.Contents.ToArray(), new[] { reimu });
            reimu.AddToRenderGroup(rg3);
            ListEq(rg2.Contents.ToArray(), new IRendered[] { });
            ListEq(rg3.Contents.ToArray(), new[] { reimu });
            reimu.LocalLocation.Value = Vector3.One;
            rg3.ZoomTarget.Value = reimu.ComputedLocalLocation;
            await rg3.ZoomTo(2f, 3f);
            return 1337;
        }

    }
    [Test]
    public void ScriptTest() {
        var s = new _TestScript();
        var t = s.Run();
        s.er.LoggedEvents.Clear();
        for (int ii = 0; !t.IsCompleted; ++ii) {
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
        }
        Assert.AreEqual(t.Result, 1337);
        ListEq(s.er.SimpleLoggedEventStrings, stored);
    }

    private static readonly string[] stored = {
        "<VNState>.$UpdateCount ~ 0",
        "<VNState>.RenderGroupCreated ~ <RenderGroup>",
        "<RenderGroup>.EntityActive ~ Active",
        "<RenderGroup>.ComputedEulerAnglesD ~ <0, 0, 0>",
        "<RenderGroup>.ComputedLocation ~ <0, 0, 0>",
        "<RenderGroup>.NestedRenderGroup ~ ",
        "<RenderGroup>.Priority ~ 2",
        "<RenderGroup>.RenderLayer ~ 0",
        "<RenderGroup>.ComputedScale ~ <1, 1, 1>",
        "<RenderGroup>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
        "<RenderGroup>.Visible ~ False",
        "<RenderGroup>.Zoom ~ 1",
        "<RenderGroup>.ZoomTarget ~ <0, 0, 0>",
        "<RenderGroup>.ZoomTransformOffset ~ <0, 0, 0>",
        "<VNState>.RenderGroupCreated ~ <RenderGroup>",
        "<RenderGroup>.EntityActive ~ Active",
        "<RenderGroup>.ComputedEulerAnglesD ~ <0, 0, 0>",
        "<RenderGroup>.ComputedLocation ~ <0, 0, 0>",
        "<RenderGroup>.NestedRenderGroup ~ ",
        "<RenderGroup>.Priority ~ 3",
        "<RenderGroup>.RenderLayer ~ 0",
        "<RenderGroup>.ComputedScale ~ <1, 1, 1>",
        "<RenderGroup>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
        "<RenderGroup>.Visible ~ False",
        "<RenderGroup>.Zoom ~ 1",
        "<RenderGroup>.ZoomTarget ~ <0, 0, 0>",
        "<RenderGroup>.ZoomTransformOffset ~ <0, 0, 0>",
        "<Reimu>.SortingID ~ 0",
        "<RenderGroup>.RendererAdded ~ <Reimu>",
        "<Reimu>.RenderGroup ~ <RenderGroup>",
        "<RenderGroup>.RendererAdded ~ <Reimu>",
        "<Reimu>.RenderGroup ~ <RenderGroup>",
        "<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<RenderGroup>.ZoomTarget ~ <1, 1, 1>",
        "<RenderGroup>.Zoom ~ 1",
        "<RenderGroup>.Zoom ~ 1",
        "<VNState>.$UpdateCount ~ 1",
        "<RenderGroup>.ZoomTransformOffset ~ <0.2, 0.2, 0.2>",
        "<RenderGroup>.Zoom ~ 1.25",
        "<VNState>.$UpdateCount ~ 2",
        "<RenderGroup>.ZoomTransformOffset ~ <0.42857143, 0.42857143, 0.42857143>",
        "<RenderGroup>.Zoom ~ 1.75",
        "<VNState>.$UpdateCount ~ 3",
        "<RenderGroup>.ZoomTransformOffset ~ <0.5, 0.5, 0.5>",
        "<RenderGroup>.Zoom ~ 2"
    };
}
}