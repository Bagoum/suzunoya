using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using NUnit.Framework;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using static Tests.AssertHelpers;
using static Suzunoya.Helpers;

namespace Tests.Suzunoya {

/// <summary>
/// Tests the autoplay skip mode, which advances confirm operations after a delay.
/// </summary>
public class _10SkipAllTest {
    public class _TestScript : TestScript {
        public _TestScript(VNState vn) : base(vn) { }
        public async Task<float> Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            await vn.Wait(0);
            vn.TimePerAutoplayConfirm = 1;
            vn.SetSkipMode(SkipMode.AUTOPLAY);
            await vn.SpinUntilConfirm();
            await reimu.MoveTo(Vector3.One, 2);
            await vn.SpinUntilConfirm();
            await reimu.MoveTo(Vector3.Zero, 2);
            await vn.SpinUntilConfirm();
            await reimu.MoveTo(Vector3.One, 2);
            await vn.SpinUntilConfirm();
            await reimu.MoveTo(Vector3.Zero, 2);
            await vn.SpinUntilConfirm();

            return 24;
        }

    }
    [Test]
    public void ScriptTest() {
        var sd = new InstanceData(new GlobalData());
        var s = new _TestScript(new VNState(Cancellable.Null, sd));
        var t = s.Run();
        s.er.LoggedEvents.Clear();
        //We play a few lines, then "quit"
        for (int ii = 0; !t.IsCompleted; ++ii) {
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
        }
        ListEq(s.er.SimpleLoggedEventStrings, stored2);
    }

    private static readonly string[] stored2 = {
        "<VNState>.$UpdateCount ~ 0",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 1",
        "<VNState>.AwaitingConfirm ~ ",
        "<VNState>.$UpdateCount ~ 2",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 3",
        "<Reimu>.ComputedLocation ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 5",
        "<VNState>.AwaitingConfirm ~ ",
        "<VNState>.$UpdateCount ~ 6",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 7",
        "<Reimu>.ComputedLocation ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 8",
        "<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 9",
        "<VNState>.AwaitingConfirm ~ ",
        "<VNState>.$UpdateCount ~ 10",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 11",
        "<Reimu>.ComputedLocation ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 12",
        "<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 13",
        "<VNState>.AwaitingConfirm ~ ",
        "<VNState>.$UpdateCount ~ 14",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 15",
        "<Reimu>.ComputedLocation ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 16",
        "<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
        "<VNState>.$UpdateCount ~ 17",
        "<VNState>.AwaitingConfirm ~ ",
        "<VNState>.$UpdateCount ~ 18"
    };
}
}