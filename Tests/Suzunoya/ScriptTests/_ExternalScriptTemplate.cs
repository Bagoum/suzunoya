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

public class _ExternalScriptTemplate {
    /// <summary>
    /// Basic example/test of how a script can be executed.
    /// </summary>
    private class _TestScript : TestScript {
        public async Task Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            //test setup
            reimu.speechCfg = SpeechSettings.Default with {
                opsPerChar = (s, i) => s[i] switch { ' ' => 1, _ => 0},
                opsPerSecond = 1,
                rollEvent = null
            };
            await vn.Wait(0);
            
            //example code
            //await reimu.Say("<color=red>hello wo<silent>rl</color>d</silent>");
            

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
        ListEq(s.er.SimpleLoggedEventStrings, stored);
    }

    private static readonly string[] stored = {
	    "<VNState>.$UpdateCount ~ 0"
    };
}
}