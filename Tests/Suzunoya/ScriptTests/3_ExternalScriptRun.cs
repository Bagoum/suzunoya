using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
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

public class _3ExternalScriptRunExample {
    /// <summary>
    /// Basic example/test of how a script can be executed.
    /// </summary>
    public class _TestScript : TestScript {
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
            reimu.Location.Value = Vector3.One;
            reimu.Alpha = 0;
            reimu.SetEmote(Emote.Happy);
            reimu.Show();
            await reimu.MoveTo(Vector3.Zero, 2, Easers.EOutSine)
	            .And(reimu.FadeTo(1, 3, Easers.ELinear));
            await vn.Wait(2);
            await reimu.Say("<color=red>hello wo<silent>rl</color>d</silent>");
            await vn.Wait(1);
            await reimu.AlsoSayN("I can make multi-part dialogue like this,");
            await vn.Wait(1);
            await reimu.AlsoSay(" or like this");
            

        }

    }
    [Test]
    public void ScriptTest() {
        var s = new _TestScript();
        EventRecord.LogEvent UpdateLog(int ii) => new(s.vn, "$UpdateCount", typeof(int), ii);
        var t = s.Run();
        s.er.LoggedEvents.Clear();
        //Mimicking some usage-specific update loop
        for (int ii = 0; !t.IsCompleted; ++ii) {
            s.er.LoggedEvents.OnNext(UpdateLog(ii));
            s.vn.Update(1f);
        }
        ListEq(s.er.SimpleLoggedEventStrings, stored);
        Assert.IsTrue(s.er.LoggedEvents.Published[21].value is Speech {Readable: "hello world"});
        Assert.IsTrue(s.er.LoggedEvents.Published[41].value is Speech {Readable: "\nI can make multi-part dialogue like this,"});
    }

    private static readonly string[] stored = {
	    "<VNState>.$UpdateCount ~ 0",
	    "<Reimu>.Location ~ <1, 1, 1>",
	    "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
	    "<Reimu>.Emotion ~ Happy",
	    "<Reimu>.Visible ~ True",
	    "<Reimu>.Location ~ <1, 1, 1>",
	    "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
	    "<Reimu>.Location ~ <1, 1, 1>",
	    "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
	    "<VNState>.$UpdateCount ~ 1",
	    "<Reimu>.Location ~ <0.29289323, 0.29289323, 0.29289323>",
	    "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.333)",
	    "<VNState>.$UpdateCount ~ 2",
	    "<Reimu>.Location ~ <0, 0, 0>",
	    "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.667)",
	    "<VNState>.$UpdateCount ~ 3",
	    "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
	    "<VNState>.$UpdateCount ~ 4",
	    "<VNState>.$UpdateCount ~ 5",
	    "<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Reimu, Default)",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.DialogueStarted ~ Suzunoya.Dialogue.Speech",
	    "<TestDialogueBox>.Dialogue ~ (TagOpen { name = color, tag = Color { color = red } }, hello)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = h }, ello)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = e }, llo)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = l }, lo)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = l }, o)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = o }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 6",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = w }, orld)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = o }, rld)",
	    "<TestDialogueBox>.Dialogue ~ (TagOpen { name = silent, tag = Silent }, rld)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = r }, ld)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = l }, d)",
	    "<TestDialogueBox>.Dialogue ~ (TagClose { opener = TagOpen { name = color, tag = Color { color = red } } }, d)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = d }, )",
	    "<TestDialogueBox>.Dialogue ~ (TagClose { opener = TagOpen { name = silent, tag = Silent } }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.$UpdateCount ~ 7",
	    "<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Reimu, DontClearText)",
	    "<TestDialogueBox>.DialogueStarted ~ Suzunoya.Dialogue.Speech",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = \n }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = I }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 8",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = c }, an)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = a }, n)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = n }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 9",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = m }, ake)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = a }, ke)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = k }, e)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = e }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 10",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = m }, ulti)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = u }, lti)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = l }, ti)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = t }, i)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = i }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = - }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = p }, art)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = a }, rt)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = r }, t)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = t }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 11",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = d }, ialogue)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = i }, alogue)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = a }, logue)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = l }, ogue)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = o }, gue)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = g }, ue)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = u }, e)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = e }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 12",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = l }, ike)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = i }, ke)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = k }, e)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = e }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 13",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = t }, his)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = h }, is)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = i }, s)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = s }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = , }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.$UpdateCount ~ 14",
	    "<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Reimu, DontClearText)",
	    "<TestDialogueBox>.DialogueStarted ~ Suzunoya.Dialogue.Speech",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 15",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = o }, r)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = r }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 16",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = l }, ike)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = i }, ke)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = k }, e)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = e }, )",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )",
	    "<VNState>.$UpdateCount ~ 17",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = t }, his)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = h }, is)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = i }, s)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = s }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()"
    };
}
}