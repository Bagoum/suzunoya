using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using NUnit.Framework;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using static Tests.Suzunoya.MyTestCharacter;
using static Tests.Suzunoya.ScriptTestHelpers;
using static Tests.AssertHelpers;
using static Suzunoya.Helpers;

namespace Tests.Suzunoya {

public class _9DisturbanceScriptTest {
    /// <summary>
    /// Tests disturbances.
    /// </summary>
    public class _TestScript : TestScript {
        public async Task<int> Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            reimu.Location.Value = Vector3.Zero;
            await reimu.MoveTo(Vector3.One, 8f, Easers.ELinear).And(
                reimu.Disturb(reimu.Location, t => new Vector3(0, 4 * OffEasers.ESoftmod010(t), 0), 4));
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
        }
        Assert.AreEqual(t.Result, 1337);
        ListEq(s.er.SimpleLoggedEventStrings, stored);
    }

    private static readonly string[] stored = {
        "<VNState>.$UpdateCount ~ 0",
        "<Reimu>.Location ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 1",
        "<Reimu>.Location ~ <0.125, 0.125, 0.125>",
        "<Reimu>.Location ~ <0.125, 2.125, 0.125>",
        "<VNState>.$UpdateCount ~ 2",
        "<Reimu>.Location ~ <0.25, 2.25, 0.25>",
        "<Reimu>.Location ~ <0.25, 4.25, 0.25>",
        "<VNState>.$UpdateCount ~ 3",
        "<Reimu>.Location ~ <0.375, 4.375, 0.375>",
        "<Reimu>.Location ~ <0.375, 2.375, 0.375>",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.Location ~ <0.5, 2.5, 0.5>",
        "<Reimu>.Location ~ <0.5, 0.5, 0.5>",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.Location ~ <0.625, 0.625, 0.625>",
        "<VNState>.$UpdateCount ~ 6",
        "<Reimu>.Location ~ <0.75, 0.75, 0.75>",
        "<VNState>.$UpdateCount ~ 7",
        "<Reimu>.Location ~ <0.875, 0.875, 0.875>",
        "<VNState>.$UpdateCount ~ 8",
        "<Reimu>.Location ~ <1, 1, 1>"
    };

}
}