﻿using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Tweening;
using NUnit.Framework;
using Suzunoya;
using Suzunoya.Entities;
using Suzunoya.Dialogue;
using Suzunoya.Display;
using static Tests.Suzunoya.MyTestCharacter;
using static Tests.Suzunoya.ScriptTestHelpers;
using static Tests.AssertHelpers;

namespace Tests.Suzunoya {

public class _1BasicVNStateFunctionalityTest {

/// <summary>
/// Tests basic functionality of VNState, adding components, Say(), and the joint record logger.
/// </summary>
    public class _TestScript : TestScript {
        public void Run() {
            RenderGroup.DefaultSortingIDStep = 1;
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            var roll = new EventRecord.LogEvent(reimu, "$SPEAK", typeof(string), "");
            reimu.speechCfg = reimu.SpeechCfg with {
                opsPerChar = (s, i) => s[i] switch { ' ' => 1, _ => 0},
                opsPerRollEvent = 1,
                rollEvent = () => er.LoggedEvents.OnNext(roll)
            };
            reimu.SetEmote(Emote.Angry);
            var t0 = reimu.Say("hEllO wOrld foo");
            var rg = vn.DefaultRenderGroup;
            ListEq(er.SimpleLoggedEventStrings, new[] {
                "<VNState>.AwaitingConfirm ~ ",
                "<VNState>.RenderGroupCreated ~ Suzunoya.Display.RenderGroup",
                "<VNState>.VNStateActive ~ True",
                "<RenderGroup>.EntityActive ~ True",
                "<RenderGroup>.EulerAnglesD ~ <0, 0, 0>",
                "<RenderGroup>.Location ~ <0, 0, 0>",
                "<RenderGroup>.Priority ~ 0",
                "<RenderGroup>.Scale ~ <1, 1, 1>",
                "<RenderGroup>.Visible ~ True",
                "<RenderGroup>.Zoom ~ 1",
                "<RenderGroup>.ZoomTarget ~ <0, 0, 0>",
                "<RenderGroup>.ZoomTransformOffset ~ <0, 0, 0>",
                "<VNState>.EntityCreated ~ Tests.Suzunoya.TestDialogueBox",
                "<TestDialogueBox>.EntityActive ~ True",
                "<TestDialogueBox>.EulerAnglesD ~ <0, 0, 0>",
                "<TestDialogueBox>.Location ~ <0, 0, 0>",
                "<TestDialogueBox>.RenderGroup ~ ",
                "<TestDialogueBox>.RenderLayer ~ 0",
                "<TestDialogueBox>.Scale ~ <1, 1, 1>",
                "<TestDialogueBox>.SortingID ~ 0",
                "<TestDialogueBox>.Speaker ~ (, Default)",
                "<TestDialogueBox>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
                "<TestDialogueBox>.Visible ~ True",
                "<TestDialogueBox>.SortingID ~ 0",
                "<RenderGroup>.RendererAdded ~ Tests.Suzunoya.TestDialogueBox",
                "<TestDialogueBox>.RenderGroup ~ Suzunoya.Display.RenderGroup",
                "<VNState>.EntityCreated ~ Tests.Suzunoya.Reimu",
                "<Reimu>.Emotion ~ Neutral",
                "<Reimu>.EntityActive ~ True",
                "<Reimu>.EulerAnglesD ~ <0, 0, 0>",
                "<Reimu>.GoheiLength ~ 14",
                "<Reimu>.Location ~ <0, 0, 0>",
                "<Reimu>.RenderGroup ~ ",
                "<Reimu>.RenderLayer ~ 0",
                "<Reimu>.Scale ~ <1, 1, 1>",
                "<Reimu>.SortingID ~ 0",
                "<Reimu>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
                "<Reimu>.Visible ~ True",
                "<Reimu>.SortingID ~ 1",
                "<RenderGroup>.RendererAdded ~ Tests.Suzunoya.Reimu",
                "<Reimu>.RenderGroup ~ Suzunoya.Display.RenderGroup",
                "<Reimu>.Emotion ~ Angry"
            });
            er.LoggedEvents.Clear();

            //The operation only starts upon "await" or retrieving this property
            var t = t0.Task;
            ListEq(er.GetAndClear(), new EventRecord.LogEvent[] {
                new(md, "Speaker", ((ICharacter)reimu, SpeakFlags.Default))
            });
            //The dT on the first frame doesn't matter since the pattern is "yield then -= dT"
            vn.Update(200f);
            ListEq(er.SimpleLoggedEventStrings, new[] {
                "<TestDialogueBox>.DialogueCleared ~ ()",
                "<TestDialogueBox>.DialogueStarted ~ Suzunoya.Dialogue.Speech",
                "<TestDialogueBox>.Dialogue ~ (Char { fragment = h }, EllO)",
                "<Reimu>.$SPEAK ~ ",
                "<TestDialogueBox>.Dialogue ~ (Char { fragment = E }, llO)",
                "<TestDialogueBox>.Dialogue ~ (Char { fragment = l }, lO)",
                "<TestDialogueBox>.Dialogue ~ (Char { fragment = l }, O)",
                "<TestDialogueBox>.Dialogue ~ (Char { fragment = O }, )",
                "<TestDialogueBox>.Dialogue ~ (Char { fragment =   }, )"
            });
            er.LoggedEvents.Clear();
            //at this point the accumulated time is 0.4 (with a 1s delay for the space)
            vn.Update(0.4f);
            Assert.AreEqual(er.LoggedEvents.Published.Count, 0);
            Assert.IsFalse(t.IsCompleted);
            vn.Update(0.8f);
            Assert.AreEqual(er.LoggedEvents.Published.Count, 7);
            vn.Update(1f);
            ListEq(er.GetAndClear().Select(x => x.ToString()).ToArray(), new[] {
                new(md, "Dialogue", (c('w'), "Orld")),
                roll,
                new(md, "Dialogue", (c('O'), "rld")),
                new(md, "Dialogue", (c('r'), "ld")),
                new(md, "Dialogue", (c('l'), "d")),
                new(md, "Dialogue", (c('d'), "")),
                new(md, "Dialogue", (c(' '), "")),
                new(md, "Dialogue", (c('f'), "oo")),
                new(md, "Dialogue", (c('o'), "o")),
                new(md, "Dialogue", (c('o'), "")),
                new(md, "DialogueFinished", Unit.Default)
            }.Select(x => x.ToString()).ToArray());
            Assert.IsTrue(t.IsCompleted);
        }

    }
    [Test]
    public void ScriptTest() {
        new _TestScript().Run();
    }
}
}