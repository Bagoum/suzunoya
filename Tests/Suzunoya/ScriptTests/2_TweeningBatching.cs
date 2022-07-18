using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using NUnit.Framework;
using Suzunoya.Entities;
using static Tests.Suzunoya.MyTestCharacter;
using static Tests.Suzunoya.ScriptTestHelpers;
using static Tests.AssertHelpers;
using static Suzunoya.Helpers;

namespace Tests.Suzunoya {

public class _2TweeningBatchingTest {
    /// <summary>
    /// Tests tweening, with VN batching.
    /// </summary>
    public class _TestScript : TestScript {
        public async Task Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            reimu.Location.Value = Vector3.Zero;
            var exc = vn.Contexts[^1];
            await vn.Wait(0);

            //No sequential batching (base case)
            var t = reimu.MoveTo(Vector3.One, 30f).Task;
            vn.RequestSkipOperation();
            await t;
            var t2 = reimu.RotateTo(Vector3.One, 5f).Task;
            await t2;
            
            ListEq(er.SimpleLoggedEventStrings, new [] {
                "<VNState>.$UpdateCount ~ 0",
                "<Reimu>.ComputedLocation ~ <0, 0, 0>",
                "<Reimu>.ComputedLocation ~ <1, 1, 1>",
                "<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
                "<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
                "<VNState>.$UpdateCount ~ 1",
                "<Reimu>.ComputedEulerAnglesD ~ <0.0954915, 0.0954915, 0.0954915>",
                "<VNState>.$UpdateCount ~ 2",
                "<Reimu>.ComputedEulerAnglesD ~ <0.34549153, 0.34549153, 0.34549153>",
                "<VNState>.$UpdateCount ~ 3",
                "<Reimu>.ComputedEulerAnglesD ~ <0.6545086, 0.6545086, 0.6545086>",
                "<VNState>.$UpdateCount ~ 4",
                "<Reimu>.ComputedEulerAnglesD ~ <0.90450853, 0.90450853, 0.90450853>",
                "<VNState>.$UpdateCount ~ 5",
                "<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>"
            });
            er.LoggedEvents.Clear();

            //Sequential batching (raw)
            var d = vn.GetOperationCanceller(out _);
            t = reimu.MoveTo(Vector3.Zero, 30f).Task;
            vn.RequestSkipOperation();
            await t;
            t2 = reimu.RotateTo(Vector3.Zero, 30f).Task;
            await t2;
            d.Dispose();
            
            
            ListEq(er.SimpleLoggedEventStrings, new [] {
                "<Reimu>.ComputedLocation ~ <1, 1, 1>",
                "<Reimu>.ComputedLocation ~ <1, 1, 1>",
                "<VNState>.$UpdateCount ~ 6",
                "<Reimu>.ComputedLocation ~ <0, 0, 0>",
                "<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>",
                "<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>"
            });
            er.LoggedEvents.Clear();
            
            //Sequential batching
            t = reimu.MoveTo(Vector3.One, 30f)
                .Then(reimu.MoveTo(Vector3.Zero, 30f)).Task;
            vn.RequestSkipOperation();
            await t;
            
            ListEq(er.SimpleLoggedEventStrings, new[] {
                "<Reimu>.ComputedLocation ~ <0, 0, 0>",
                "<Reimu>.ComputedLocation ~ <0, 0, 0>",
                "<VNState>.$UpdateCount ~ 7",
                "<Reimu>.ComputedLocation ~ <1, 1, 1>",
                "<Reimu>.ComputedLocation ~ <1, 1, 1>",
                "<Reimu>.ComputedLocation ~ <0, 0, 0>"
            });
            er.LoggedEvents.Clear();
            
            //Parallel batching
            t = reimu.MoveTo(Vector3.One, 30f)
                .And(reimu.RotateTo(Vector3.One, 30f)).Task;
            t2 = vn.Wait(30).Task;
            vn.RequestSkipOperation();
            await t2;
            await t;

            ListEq(er.SimpleLoggedEventStrings, new[] {
                "<Reimu>.ComputedLocation ~ <0, 0, 0>",
                "<Reimu>.ComputedLocation ~ <0, 0, 0>",
                "<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
                "<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
                "<VNState>.$UpdateCount ~ 8",
                "<Reimu>.ComputedLocation ~ <1, 1, 1>",
                "<Reimu>.ComputedEulerAnglesD ~ <1, 1, 1>"
            });
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
    }
}
}