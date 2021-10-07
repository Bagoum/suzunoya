using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
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

public class _12BoundedContextTest {
    public class _TestScript : TestScript {
	    public _TestScript(VNState? vn = null) : base(vn) { }
	    public BoundedContext<int> Run() => new(vn, "l1", async () => {
		    var md = vn.Add(new TestDialogueBox());
		    var reimu = vn.Add(new Reimu());
		    //test setup
		    reimu.speechCfg = SpeechSettings.Default with {
			    opsPerChar = (s, i) => 1,
			    opsPerSecond = 1,
			    rollEvent = null
		    };
		    await vn.Wait(0);
		    await reimu.Say("12345");
		    var innerVal = await InnerCtx().Execute();
		    await reimu.Say("67890123");
		    await reimu.Say("4567");
		    return 25 + innerVal;
	    });

	    private BoundedContext<int> InnerCtx() => new(vn, "l2", async () => {
		    var yukari = vn.Add(new Yukari());
		    yukari.speechCfg = SpeechSettings.Default with {
			    opsPerChar = (s, i) => 1,
			    opsPerSecond = 1,
			    rollEvent = null
		    };
		    await yukari.Say("12345");
		    await yukari.Say("6789");
		    yukari.Delete();
		    return 24;
	    });

    }
    [Test]
    public void ScriptTest() {
        var s = new _TestScript(new VNState(Cancellable.Null, new InstanceData()));
        EventRecord.LogEvent UpdateLog(int ii) => new(s.vn, "$UpdateCount", typeof(int), ii);
        var t = s.Run().Execute().Task;
        s.er.LoggedEvents.Clear();
        for (int ii = 0; ii < 7; ++ii) {
            s.er.LoggedEvents.OnNext(UpdateLog(ii));
            s.vn.Update(1f);
        }
        ListEq(s.er.SimpleLoggedEventStrings, stored);
        var sd = s.vn.UpdateSavedata();
        s.er.LoggedEvents.Clear();
        //7 frames puts us in the middle of the inner context. Load it back and step again.
        //Note: even though after 7 frames we are in the process of loading Yukari's "12345" string,
        // when we load, the "12345" string will be skipped.
        // This is because the "last operation" is also skipped when loading.

        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Run().Execute().Task;
        for (int ii = 0; ii < 8; ++ii) {
	        s.er.LoggedEvents.OnNext(UpdateLog(ii));
	        s.vn.Update(1f);
        }
        ListEq(s.er.SimpleLoggedEventStrings, stored2);
        var sd2 = s.vn.UpdateSavedata();
        s.er.LoggedEvents.Clear();
        
        //Now we are past the inner context. If we reload, the inner context should never get evaluated.
        s = new _TestScript(new VNState(Cancellable.Null, sd2));
        t = s.Run().Execute().Task;
        for (int ii = 0; !t.IsCompleted; ++ii) {
	        s.er.LoggedEvents.OnNext(UpdateLog(ii));
	        s.vn.Update(1f);
        }
        Assert.AreEqual(t.Result, 49);
        ListEq(s.er.SimpleLoggedEventStrings, stored3);
    }

    private static readonly string[] stored = {
	    "<VNState>.$UpdateCount ~ 0",
	    "<VNState>.CurrentOperationID ~ 12345",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Reimu, Default)",
	    "<TestDialogueBox>.DialogueStarted ~ Reimu:12345",
	    "<VNState>.DialogueLog ~ Reimu:12345",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 2345)",
	    "<VNState>.$UpdateCount ~ 1",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 345)",
	    "<VNState>.$UpdateCount ~ 2",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, 45)",
	    "<VNState>.$UpdateCount ~ 3",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 4 }, 5)",
	    "<VNState>.$UpdateCount ~ 4",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 5 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.CurrentOperationID ~ $$__OPEN__$$::l2",
	    "<VNState>.ContextStarted ~ Context:l2",
	    "<VNState>.EntityCreated ~ Tests.Suzunoya.Yukari",
	    "<Yukari>.Emotion ~ Neutral",
	    "<Yukari>.EntityActive ~ True",
	    "<Yukari>.EulerAnglesD ~ <0, 0, 0>",
	    "<Yukari>.Location ~ <0, 0, 0>",
	    "<Yukari>.PositionOfYukarisChair ~ <2, 3>",
	    "<Yukari>.RenderGroup ~ ",
	    "<Yukari>.RenderLayer ~ 0",
	    "<Yukari>.Scale ~ <1, 1, 1>",
	    "<Yukari>.SortingID ~ 0",
	    "<Yukari>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
	    "<Yukari>.Visible ~ True",
	    "<Yukari>.SortingID ~ 20",
	    "<RenderGroup>.RendererAdded ~ Tests.Suzunoya.Yukari",
	    "<Yukari>.RenderGroup ~ Suzunoya.Display.RenderGroup",
	    "<VNState>.CurrentOperationID ~ 12345",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Yukari, Default)",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:12345",
	    "<VNState>.DialogueLog ~ Nobody:12345",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 2345)",
	    "<VNState>.$UpdateCount ~ 5",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 345)",
	    "<VNState>.$UpdateCount ~ 6",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, 45)"
    };
    public static readonly string[] stored2 = {
	    "<VNState>.AwaitingConfirm ~ ",
	    "<VNState>.CurrentOperationID ~ $$__OPEN__$$",
	    "<VNState>.InputAllowed ~ True",
	    "<VNState>.RenderGroupCreated ~ Suzunoya.Display.RenderGroup",
	    "<VNState>.VNStateActive ~ True",
	    "<RenderGroup>.EntityActive ~ True",
	    "<RenderGroup>.EulerAnglesD ~ <0, 0, 0>",
	    "<RenderGroup>.Location ~ <0, 0, 0>",
	    "<RenderGroup>.NestedRenderGroup ~ ",
	    "<RenderGroup>.Priority ~ 0",
	    "<RenderGroup>.RenderLayer ~ 0",
	    "<RenderGroup>.Scale ~ <1, 1, 1>",
	    "<RenderGroup>.Tint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
	    "<RenderGroup>.Visible ~ True",
	    "<RenderGroup>.Zoom ~ 1",
	    "<RenderGroup>.ZoomTarget ~ <0, 0, 0>",
	    "<RenderGroup>.ZoomTransformOffset ~ <0, 0, 0>",
	    "<VNState>.CurrentOperationID ~ $$__OPEN__$$::l1",
	    "<VNState>.ContextStarted ~ Context:l1",
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
	    "<Reimu>.SortingID ~ 10",
	    "<RenderGroup>.RendererAdded ~ Tests.Suzunoya.Reimu",
	    "<Reimu>.RenderGroup ~ Suzunoya.Display.RenderGroup",
	    "<VNState>.$UpdateCount ~ 0",
	    "<VNState>.CurrentOperationID ~ 12345",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Reimu, Default)",
	    "<TestDialogueBox>.DialogueStarted ~ Reimu:12345",
	    "<VNState>.DialogueLog ~ Reimu:12345",
	    //Reimu's 12345 gets skipped
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 2345)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 345)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, 45)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 4 }, 5)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 5 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.CurrentOperationID ~ $$__OPEN__$$::l2",
	    "<VNState>.ContextStarted ~ Context:l2",
	    "<VNState>.EntityCreated ~ Tests.Suzunoya.Yukari",
	    "<Yukari>.Emotion ~ Neutral",
	    "<Yukari>.EntityActive ~ True",
	    "<Yukari>.EulerAnglesD ~ <0, 0, 0>",
	    "<Yukari>.Location ~ <0, 0, 0>",
	    "<Yukari>.PositionOfYukarisChair ~ <2, 3>",
	    "<Yukari>.RenderGroup ~ ",
	    "<Yukari>.RenderLayer ~ 0",
	    "<Yukari>.Scale ~ <1, 1, 1>",
	    "<Yukari>.SortingID ~ 0",
	    "<Yukari>.Tint ~ RGBA(1.000, 1.000, 1.000, 0.000)",
	    "<Yukari>.Visible ~ True",
	    "<Yukari>.SortingID ~ 20",
	    "<RenderGroup>.RendererAdded ~ Tests.Suzunoya.Yukari",
	    "<Yukari>.RenderGroup ~ Suzunoya.Display.RenderGroup",
	    "<VNState>.CurrentOperationID ~ 12345",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Yukari, Default)",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:12345",
	    "<VNState>.DialogueLog ~ Nobody:12345",
	    //Yukari's 12345 also gets skipped
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 2345)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 345)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, 45)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 4 }, 5)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 5 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.CurrentOperationID ~ 6789",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Yukari, Default)",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:6789",
	    "<VNState>.DialogueLog ~ Nobody:6789",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 6 }, 789)",
	    "<VNState>.$UpdateCount ~ 1",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 7 }, 89)",
	    "<VNState>.$UpdateCount ~ 2",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 8 }, 9)",
	    "<VNState>.$UpdateCount ~ 3",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 9 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<Yukari>.EntityActive ~ False",
	    "<VNState>.ContextFinished ~ Context:l2",
	    "<VNState>.CurrentOperationID ~ 67890123",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Reimu, Default)",
	    "<TestDialogueBox>.DialogueStarted ~ Reimu:67890123",
	    "<VNState>.DialogueLog ~ Reimu:67890123",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 6 }, 7890123)",
	    "<VNState>.$UpdateCount ~ 4",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 7 }, 890123)",
	    "<VNState>.$UpdateCount ~ 5",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 8 }, 90123)",
	    "<VNState>.$UpdateCount ~ 6",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 9 }, 0123)",
	    "<VNState>.$UpdateCount ~ 7",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 0 }, 123)"
    };

    public static readonly string[] stored3 = {
		"<VNState>.AwaitingConfirm ~ ",
		"<VNState>.CurrentOperationID ~ $$__OPEN__$$",
		"<VNState>.InputAllowed ~ True",
		"<VNState>.RenderGroupCreated ~ Suzunoya.Display.RenderGroup",
		"<VNState>.VNStateActive ~ True",
		"<RenderGroup>.EntityActive ~ True",
		"<RenderGroup>.EulerAnglesD ~ <0, 0, 0>",
		"<RenderGroup>.Location ~ <0, 0, 0>",
		"<RenderGroup>.NestedRenderGroup ~ ",
		"<RenderGroup>.Priority ~ 0",
		"<RenderGroup>.RenderLayer ~ 0",
		"<RenderGroup>.Scale ~ <1, 1, 1>",
		"<RenderGroup>.Tint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
		"<RenderGroup>.Visible ~ True",
		"<RenderGroup>.Zoom ~ 1",
		"<RenderGroup>.ZoomTarget ~ <0, 0, 0>",
		"<RenderGroup>.ZoomTransformOffset ~ <0, 0, 0>",
		"<VNState>.CurrentOperationID ~ $$__OPEN__$$::l1",
		"<VNState>.ContextStarted ~ Context:l1",
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
		"<Reimu>.SortingID ~ 10",
		"<RenderGroup>.RendererAdded ~ Tests.Suzunoya.Reimu",
		"<Reimu>.RenderGroup ~ Suzunoya.Display.RenderGroup",
		"<VNState>.$UpdateCount ~ 0",
		"<VNState>.CurrentOperationID ~ 12345",
		"<TestDialogueBox>.DialogueCleared ~ ()",
		"<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Reimu, Default)",
		"<TestDialogueBox>.DialogueStarted ~ Reimu:12345",
		"<VNState>.DialogueLog ~ Reimu:12345",
		//Reimu's 12345 gets skipped
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 2345)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 345)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, 45)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 4 }, 5)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 5 }, )",
		"<TestDialogueBox>.DialogueFinished ~ ()",
		//The inner context is constructed and immediately destroyed without constructing Yukari.
		//This is the major computational benefit of bounded contexts w.r.t loading/backlogging.
		"<VNState>.CurrentOperationID ~ $$__OPEN__$$::l2",
		"<VNState>.ContextStarted ~ Context:l2",
		"<VNState>.ContextFinished ~ Context:l2",
		"<VNState>.CurrentOperationID ~ 67890123",
		"<TestDialogueBox>.DialogueCleared ~ ()",
		"<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Reimu, Default)",
		"<TestDialogueBox>.DialogueStarted ~ Reimu:67890123",
		"<VNState>.DialogueLog ~ Reimu:67890123",
		//Reimu's 67890123 get skipped.
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 6 }, 7890123)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 7 }, 890123)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 8 }, 90123)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 9 }, 0123)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 0 }, 123)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 23)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 3)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, )",
		"<TestDialogueBox>.DialogueFinished ~ ()",
		"<VNState>.CurrentOperationID ~ 4567",
		"<TestDialogueBox>.DialogueCleared ~ ()",
		"<TestDialogueBox>.Speaker ~ (Tests.Suzunoya.Reimu, Default)",
		"<TestDialogueBox>.DialogueStarted ~ Reimu:4567",
		"<VNState>.DialogueLog ~ Reimu:4567",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 4 }, 567)",
		"<VNState>.$UpdateCount ~ 1",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 5 }, 67)",
		"<VNState>.$UpdateCount ~ 2",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 6 }, 7)",
		"<VNState>.$UpdateCount ~ 3",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 7 }, )",
		"<TestDialogueBox>.DialogueFinished ~ ()",
		"<VNState>.ContextFinished ~ Context:l1"
    };
}
}