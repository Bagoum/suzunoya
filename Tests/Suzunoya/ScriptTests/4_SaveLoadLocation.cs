using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using NUnit.Framework;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Dialogue;
using Suzunoya.Entities;
using static Tests.Suzunoya.MyTestCharacter;
using static Tests.Suzunoya.ScriptTestHelpers;
using static Tests.AssertHelpers;
using static Suzunoya.Helpers;

namespace Tests.Suzunoya {

public class _4SaveLoadLocationTest {
    private class _TestScript : TestScript {
        public _TestScript(VNState vn) : base(vn) { }

        public BoundedContext<int> Run() => new(vn, "test4", async () => {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            await vn.Wait(0);

            reimu.Alpha = 0;
            vn.OperationID.OnNext("A");
            await reimu.MoveTo(Vector3.One, 2f).Then(
                reimu.MoveTo(Vector3.One * 2, 3f).And(
                    reimu.FadeTo(1f, 5f))
            ).C;
            vn.OperationID.OnNext("B");
            await reimu.RotateTo(Vector3.One, 2f);
            vn.OperationID.OnNext("C");
            await reimu.MoveTo(Vector3.Zero, 1f);
            vn.OperationID.OnNext("D");
            await reimu.ScaleTo(Vector3.One * 3, 2f);
            vn.OperationID.OnNext("E");
            await reimu.RotateTo(Vector3.Zero, 4f);
            vn.OperationID.OnNext("F");
            return 5;
        });

    }
    [Test]
    public void ScriptTest() {
        var sd = new InstanceData(new GlobalData());
        var s = new _TestScript(new VNState(Cancellable.Null, sd));
        var t = s.Run().Execute();
        s.er.LoggedEvents.Clear();
        VNLocation loc = null!;
        //We play a few lines, then "quit"
        for (int ii = 0; ii < 12; ++ii) {
            bool sendConfirm = s.vn.AwaitingConfirm.Value != null;
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
            if (sendConfirm)
                s.vn.UserConfirm();
            if (ii == 10)
                loc = VNLocation.Make(s.vn)!;
        }
        s.vn.UpdateInstanceData();
        s.vn.DeleteAll();
        Assert.AreEqual(loc, new VNLocation("B", new List<string>(){ "test4" }));
        Assert.AreEqual(sd.Location, new VNLocation("C", new List<string>(){ "test4" }));
        Assert.IsTrue(new[]{"A", "B", "C",}.All(sd.GlobalData.IsLineRead));
        Assert.IsFalse(new[]{"D", "E", "F"}.Any(sd.GlobalData.IsLineRead));
        ListEq(s.er.SimpleLoggedEventStrings, stored1);
        sd.Location = loc;
        //Then we load again
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Run().Execute();
        s.er.LoggedEvents.Clear();
        for (int ii = 0; !t.IsCompleted; ++ii) {
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
        }
        Assert.AreEqual(t.Result, 5);
        ListEq(s.er.SimpleLoggedEventStrings, stored2);
    }

    private static readonly string[] stored1 = {
        "<VNState>.$UpdateCount ~ 0",
        "<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        "<VNState>.OperationID ~ A",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 1",
        "<Reimu>.ComputedLocation ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 2",
        "<Reimu>.ComputedLocation ~ <1, 1, 1>",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        //"<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        //"<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        "<VNState>.$UpdateCount ~ 3",
        "<Reimu>.ComputedLocation ~ <1.25, 1.25, 1.25>",
        "<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 0.095)",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.ComputedLocation ~ <1.75, 1.75, 1.75>",
        "<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 0.345)",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.ComputedLocation ~ <2, 2, 2>",
        "<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 0.655)",
        "<VNState>.$UpdateCount ~ 6",
        "<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 0.905)",
        "<VNState>.$UpdateCount ~ 7",
        "<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 8",
        "<VNState>.AwaitingConfirm ~ ", //null
        "<VNState>.$UpdateCount ~ 9",
        "<VNState>.OperationID ~ B",
        //"<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
        //"<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>", //Location is captured after this command.
        "<VNState>.$UpdateCount ~ 10",
        "<Reimu>.ComputedEulerAnglesD ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 11",
        "<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>",
        "<VNState>.OperationID ~ C",
        //"<Reimu>.ComputedLocation ~ <2, 2, 2>",
        //"<Reimu>.ComputedLocation ~ <2, 2, 2>", //The last process is the beginning of the move location from 2 to 0.
        "<TestDialogueBox>.EntityActive ~ False",
        "<VNState>.ContextFinished ~ Context:test4",
        "<Reimu>.EntityActive ~ False",
        "<RenderGroup>.EntityActive ~ False",
        "<VNState>.VNStateActive ~ False"
    };

    private static readonly string[] stored2 = {
        "<VNState>.$UpdateCount ~ 0",
        "<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        "<VNState>.OperationID ~ A",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<Reimu>.ComputedLocation ~ <1, 1, 1>",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<Reimu>.ComputedLocation ~ <2, 2, 2>",
        //"<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        "<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
        "<VNState>.OperationID ~ B",
        //"<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
        //"<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 1",
        "<Reimu>.ComputedEulerAnglesD ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 2",
        "<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>",
        "<VNState>.OperationID ~ C",
       // "<Reimu>.ComputedLocation ~ <2, 2, 2>",
        //"<Reimu>.ComputedLocation ~ <2, 2, 2>",
        "<VNState>.$UpdateCount ~ 3",
        "<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.OperationID ~ D",
        //"<Reimu>.ComputedScale ~ <1, 1, 1>",
        //"<Reimu>.ComputedScale ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.ComputedScale ~ <2, 2, 2>",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.ComputedScale ~ <3, 3, 3>",
        "<VNState>.OperationID ~ E",
        //"<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>",
        //"<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 6",
        "<Reimu>.ComputedEulerAnglesD ~ <0.8535534, 0.8535534, 0.8535534>",
        "<VNState>.$UpdateCount ~ 7",
        "<Reimu>.ComputedEulerAnglesD ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 8",
        "<Reimu>.ComputedEulerAnglesD ~ <0.14644659, 0.14644659, 0.14644659>",
        "<VNState>.$UpdateCount ~ 9",
        "<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
        "<VNState>.OperationID ~ F",
        "<VNState>.ContextFinished ~ Context:test4",
    };
}
}