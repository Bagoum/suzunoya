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

public class _4SaveLoadTest {
    /// <summary>
    /// Basic example/test of how a script can be executed.
    /// </summary>
    public class _TestScript : TestScript {
        public _TestScript(VNState vn) : base(vn) { }
        public async Task Run() {
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
        }

    }
    [Test]
    public void ScriptTest() {
        var sd = new InstanceData(new GlobalData());
        var s = new _TestScript(new VNState(Cancellable.Null, sd));
        var t = s.Run();
        s.er.LoggedEvents.Clear();
        //We play a few lines, then "quit"
        for (int ii = 0; ii < 12; ++ii) {
            bool sendConfirm = s.vn.AwaitingConfirm.Value != null;
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
            if (sendConfirm)
                s.vn.UserConfirm();
        }
        s.vn.UpdateSavedata();
        Assert.AreEqual(sd.Location, new VNLocation("C", new List<string>()));
        Assert.IsTrue(new[]{"A", "B", "C",}.All(sd.GlobalData.IsLineRead));
        Assert.IsFalse(new[]{"D", "E", "F"}.Any(sd.GlobalData.IsLineRead));
        ListEq(s.er.SimpleLoggedEventStrings, stored1);
        //Then we load again
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Run();
        s.er.LoggedEvents.Clear();
        for (int ii = 0; !t.IsCompleted; ++ii) {
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
        }
        ListEq(s.er.SimpleLoggedEventStrings, stored2);
    }

    private static readonly string[] stored1 = {
        "<VNState>.$UpdateCount ~ 0",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        "<VNState>.OperationID ~ A",
        "<Reimu>.Location ~ <0, 0, 0>",
        "<Reimu>.Location ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 1",
        "<Reimu>.Location ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 2",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        "<VNState>.$UpdateCount ~ 3",
        "<Reimu>.Location ~ <1.25, 1.25, 1.25>",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.095)",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.Location ~ <1.75, 1.75, 1.75>",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.345)",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.Location ~ <2, 2, 2>",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.655)",
        "<VNState>.$UpdateCount ~ 6",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.905)",
        "<VNState>.$UpdateCount ~ 7",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 8",
        "<VNState>.AwaitingConfirm ~ ", //null
        "<VNState>.$UpdateCount ~ 9",
        "<VNState>.OperationID ~ B",
        "<Reimu>.EulerAnglesD ~ <0, 0, 0>",
        "<Reimu>.EulerAnglesD ~ <0, 0, 0>", //Location is captured after this command.
        "<VNState>.$UpdateCount ~ 10",
        "<Reimu>.EulerAnglesD ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 11",
        "<Reimu>.EulerAnglesD ~ <1, 1, 1>",
        "<VNState>.OperationID ~ C",
        "<Reimu>.Location ~ <2, 2, 2>",
        "<Reimu>.Location ~ <2, 2, 2>", //The last process is the beginning of the move location from 2 to 0.
    };

    private static readonly string[] stored2 = {
        "<VNState>.$UpdateCount ~ 0",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        "<VNState>.OperationID ~ A",
        "<Reimu>.Location ~ <0, 0, 0>",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<Reimu>.Location ~ <2, 2, 2>",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
        "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
        "<VNState>.OperationID ~ B",
        "<Reimu>.EulerAnglesD ~ <0, 0, 0>",
        "<Reimu>.EulerAnglesD ~ <1, 1, 1>",
        "<VNState>.OperationID ~ C",
        "<Reimu>.Location ~ <2, 2, 2>",
        "<Reimu>.Location ~ <2, 2, 2>", 
        //Everything until the last process (move location from 2 to 0) gets hypersped.
        //Note that it may not always take zero frames. At most, it might take one frame per operation
        // (which is effectively instantaneous in basically all use cases).
        "<VNState>.$UpdateCount ~ 1",
        //From here, everything is normal.
        "<Reimu>.Location ~ <0, 0, 0>",
        "<VNState>.OperationID ~ D",
        "<Reimu>.Scale ~ <1, 1, 1>",
        "<Reimu>.Scale ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 2",
        "<Reimu>.Scale ~ <2, 2, 2>",
        "<VNState>.$UpdateCount ~ 3",
        "<Reimu>.Scale ~ <3, 3, 3>",
        "<VNState>.OperationID ~ E",
        "<Reimu>.EulerAnglesD ~ <1, 1, 1>",
        "<Reimu>.EulerAnglesD ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.EulerAnglesD ~ <0.8535534, 0.8535534, 0.8535534>",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.EulerAnglesD ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 6",
        "<Reimu>.EulerAnglesD ~ <0.14644659, 0.14644659, 0.14644659>",
        "<VNState>.$UpdateCount ~ 7",
        "<Reimu>.EulerAnglesD ~ <0, 0, 0>",
        "<VNState>.OperationID ~ F"
    };
}
}