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

public class _5CancellationTest {
    private class _TestScript : TestScript {
        public async Task<int> Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            await vn.Wait(0);

            await vn.Wait(12);
            await reimu.MoveTo(Vector3.One, 2);
            await vn.SpinUntilConfirm();

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
            if (ii == 2)
                s.vn.RequestSkipOperation();
            if (ii == 7)
                s.vn.UserConfirm();
        }
        Assert.AreEqual(t.Result, 1337);
        ListEq(s.er.SimpleLoggedEventStrings, stored);
    }

    private static readonly string[] stored = {
        "<VNState>.$UpdateCount ~ 0",
        "<VNState>.$UpdateCount ~ 1",
        "<VNState>.$UpdateCount ~ 2",
        //wait(12) is softskipped here
        "<VNState>.$UpdateCount ~ 3",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.ComputedLocation ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 6",
        "<VNState>.$UpdateCount ~ 7",
        "<VNState>.AwaitingConfirm ~ ",
        "<VNState>.$UpdateCount ~ 8",
    };
}
}