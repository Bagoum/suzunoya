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
using Suzunoya.Entities;
using static Tests.Suzunoya.MyTestCharacter;
using static Tests.Suzunoya.ScriptTestHelpers;
using static Tests.AssertHelpers;
using static Suzunoya.Helpers;

namespace Tests.Suzunoya {

public class _6ConfirmTest {
    private class _TestScript : TestScript {
        public async Task<int> Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            await vn.Wait(0);

            await reimu.MoveTo(Vector3.One, 4).C;
            await reimu.RotateTo(Vector3.One, 2).Then(reimu.ScaleTo(Vector3.Zero, 3)).C;

            await reimu.MoveTo(Vector3.Zero, 3).And(
                reimu.RotateTo(Vector3.Zero, 2)
            ).C;

            return 1337;
        }

    }
    [Test]
    public void ScriptTest() {
        var s = new _TestScript();
        var t = s.Run();
        s.er.LoggedEvents.Clear();
        //Mimicking some usage-specific update loop
        for (int ii = 0; !t.IsCompleted; ++ii) {
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
            if (ii == 6 || ii == 10 || ii == 14 || ii == 20)
                s.vn.UserConfirm();
        }
        Assert.AreEqual(t.Result, 1337);
        ListEq(s.er.SimpleLoggedEventStrings, stored);
    }

    private static readonly string[] stored = {
        "<VNState>.$UpdateCount ~ 0",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 1",
        "<Reimu>.ComputedLocation ~ <0.14644662, 0.14644662, 0.14644662>",
        "<VNState>.$UpdateCount ~ 2",
        "<Reimu>.ComputedLocation ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 3",
        "<Reimu>.ComputedLocation ~ <0.8535534, 0.8535534, 0.8535534>",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 5",
        "<VNState>.$UpdateCount ~ 6",
        "<VNState>.AwaitingConfirm ~ ",
        "<VNState>.$UpdateCount ~ 7",
        //"<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
        //"<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 8",
        "<Reimu>.ComputedEulerAnglesD ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 9",
        "<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>",
        //"<Reimu>.ComputedScale ~ <1, 1, 1>",
        //"<Reimu>.ComputedScale ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 10",
        // Confirm input here has no effect
        "<Reimu>.ComputedScale ~ <0.75, 0.75, 0.75>",
        "<VNState>.$UpdateCount ~ 11",
        "<Reimu>.ComputedScale ~ <0.25, 0.25, 0.25>",
        "<VNState>.$UpdateCount ~ 12",
        "<Reimu>.ComputedScale ~ <0, 0, 0>",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 13",
        "<VNState>.$UpdateCount ~ 14",
        "<VNState>.AwaitingConfirm ~ ",
        "<VNState>.$UpdateCount ~ 15",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        //"<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        //"<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 16",
        "<Reimu>.ComputedLocation ~ <0.75, 0.75, 0.75>",
        "<Reimu>.ComputedEulerAnglesD ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 17",
        "<Reimu>.ComputedLocation ~ <0.25, 0.25, 0.25>",
        "<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 18",
        "<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 19",
        "<VNState>.$UpdateCount ~ 20",
        "<VNState>.AwaitingConfirm ~ ",
        "<VNState>.$UpdateCount ~ 21"
    };
}
}