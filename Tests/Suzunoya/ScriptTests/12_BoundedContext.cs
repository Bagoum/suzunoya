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
    private class _TestScript : TestScript {
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
		    var innerVal = await InnerCtx();
		    await reimu.Say("67890123");
		    await reimu.Say("4567");
		    return 25 + innerVal;
	    });

	    private StrongBoundedContext<int> InnerCtx() => new(vn, "l2", async () => {
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
        var s = new _TestScript(new VNState(Cancellable.Null, new InstanceData(new GlobalData())));
        EventRecord.LogEvent UpdateLog(int ii) => new(s.vn, "$UpdateCount", typeof(int), ii);
        var t = s.Run().Execute();
        s.er.LoggedEvents.Clear();
        for (int ii = 0; ii < 7; ++ii) {
            s.er.LoggedEvents.OnNext(UpdateLog(ii));
            s.vn.Update(1f);
        }
        ListEq(s.er.SimpleLoggedEventStrings, stored);
        var sd = s.vn.UpdateInstanceData();
        s.er.LoggedEvents.Clear();
        //7 frames puts us in the middle of the inner context. Load it back and step again.
        //Note: even though after 7 frames we are in the process of loading Yukari's "12345" string,
        // when we load, the "12345" string will be skipped.
        // This is because the "last operation" is also skipped when loading.

        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Run().Execute();
        for (int ii = 0; ii < 8; ++ii) {
	        s.er.LoggedEvents.OnNext(UpdateLog(ii));
	        s.vn.Update(1f);
        }
        ListEq(s.er.SimpleLoggedEventStrings, stored2);
        var sd2 = s.vn.UpdateInstanceData();
        s.er.LoggedEvents.Clear();
        
        //Now we are past the inner context. If we reload, the inner context should never get evaluated.
        s = new _TestScript(new VNState(Cancellable.Null, sd2));
        t = s.Run().Execute();
        for (int ii = 0; !t.IsCompleted; ++ii) {
	        s.er.LoggedEvents.OnNext(UpdateLog(ii));
	        s.vn.Update(1f);
        }
        Assert.AreEqual(t.Result, 49);
        ListEq(s.er.SimpleLoggedEventStrings, stored3);
    }

    private static readonly string[] stored = {
	    "<VNState>.$UpdateCount ~ 0",
	    "<VNState>.OperationID ~ 12345",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (<Reimu>, Default)",
	    "<VNState>.DialogueLog ~ Reimu:12345",
	    "<TestDialogueBox>.DialogueStarted ~ Reimu:12345",
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
	    "<VNState>.ContextStarted ~ StrongContext:l2",
	    "<VNState>.OperationID ~ $$__OPEN__$$::l2",
	    "<RenderGroup>.RendererAdded ~ <Yukari>",
	    "<VNState>.EntityCreated ~ <Yukari>",
	    "<Yukari>.Emote ~ ",
	    "<Yukari>.Emotion ~ Neutral",
	    "<Yukari>.EntityActive ~ True",
	    "<Yukari>.ComputedEulerAnglesD ~ <0, 0, 0>",
	    "<Yukari>.ComputedLocation ~ <0, 0, 0>",
	    "<Yukari>.PositionOfYukarisChair ~ <2, 3>",
	    "<Yukari>.RenderGroup ~ <RenderGroup>",
	    "<Yukari>.RenderLayer ~ 0",
	    "<Yukari>.ComputedScale ~ <1, 1, 1>",
	    "<Yukari>.SortingID ~ 20",
	    "<Yukari>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
	    "<Yukari>.Visible ~ True",
	    "<VNState>.OperationID ~ 12345",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (<Yukari>, Default)",
	    "<VNState>.DialogueLog ~ Nobody:12345",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:12345",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 2345)",
	    "<VNState>.$UpdateCount ~ 5",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 345)",
	    "<VNState>.$UpdateCount ~ 6",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, 45)"
    };
    public static readonly string[] stored2 = {
	    "<VNState>.AwaitingConfirm ~ ",
	    "<VNState>.InputAllowed ~ True",
	    "<VNState>.OperationID ~ $$__OPEN__$$",
	    "<VNState>.RenderGroupCreated ~ <RenderGroup>",
	    "<VNState>.VNStateActive ~ True",
	    "<RenderGroup>.EntityActive ~ True",
	    "<RenderGroup>.ComputedEulerAnglesD ~ <0, 0, 0>",
	    "<RenderGroup>.ComputedLocation ~ <0, 0, 0>",
	    "<RenderGroup>.NestedRenderGroup ~ ",
	    "<RenderGroup>.Priority ~ 0",
	    "<RenderGroup>.RenderLayer ~ 0",
	    "<RenderGroup>.ComputedScale ~ <1, 1, 1>",
	    "<RenderGroup>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
	    "<RenderGroup>.Visible ~ True",
	    "<RenderGroup>.Zoom ~ 1",
	    "<RenderGroup>.ZoomTarget ~ <0, 0, 0>",
	    "<RenderGroup>.ZoomTransformOffset ~ <0, 0, 0>",
	    "<VNState>.ContextStarted ~ Context:l1",
	    "<VNState>.OperationID ~ $$__OPEN__$$::l1",
	    "<RenderGroup>.RendererAdded ~ <TestDialogueBox>",
	    "<VNState>.EntityCreated ~ <TestDialogueBox>",
	    "<TestDialogueBox>.EntityActive ~ True",
	    "<TestDialogueBox>.ComputedEulerAnglesD ~ <0, 0, 0>",
	    "<TestDialogueBox>.ComputedLocation ~ <0, 0, 0>",
	    "<TestDialogueBox>.RenderGroup ~ <RenderGroup>",
	    "<TestDialogueBox>.RenderLayer ~ 0",
	    "<TestDialogueBox>.ComputedScale ~ <1, 1, 1>",
	    "<TestDialogueBox>.SortingID ~ 0",
	    "<TestDialogueBox>.Speaker ~ (, Default)",
	    "<TestDialogueBox>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
	    "<TestDialogueBox>.Visible ~ True",
	    "<RenderGroup>.RendererAdded ~ <Reimu>",
	    "<VNState>.EntityCreated ~ <Reimu>",
	    "<Reimu>.Emote ~ ",
	    "<Reimu>.Emotion ~ Neutral",
	    "<Reimu>.EntityActive ~ True",
	    "<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
	    "<Reimu>.GoheiLength ~ 14",
	    "<Reimu>.ComputedLocation ~ <0, 0, 0>",
	    "<Reimu>.RenderGroup ~ <RenderGroup>",
	    "<Reimu>.RenderLayer ~ 0",
	    "<Reimu>.ComputedScale ~ <1, 1, 1>",
	    "<Reimu>.SortingID ~ 10",
	    "<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
	    "<Reimu>.Visible ~ True",
	    "<VNState>.$UpdateCount ~ 0",
	    "<VNState>.OperationID ~ 12345",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (<Reimu>, Default)",
	    "<VNState>.DialogueLog ~ Reimu:12345",
	    "<TestDialogueBox>.DialogueStarted ~ Reimu:12345",
	    //Reimu's 12345 gets skipped
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 2345)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 345)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, 45)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 4 }, 5)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 5 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.ContextStarted ~ StrongContext:l2",
	    "<VNState>.OperationID ~ $$__OPEN__$$::l2",
	    "<RenderGroup>.RendererAdded ~ <Yukari>",
	    "<VNState>.EntityCreated ~ <Yukari>",
	    "<Yukari>.Emote ~ ",
	    "<Yukari>.Emotion ~ Neutral",
	    "<Yukari>.EntityActive ~ True",
	    "<Yukari>.ComputedEulerAnglesD ~ <0, 0, 0>",
	    "<Yukari>.ComputedLocation ~ <0, 0, 0>",
	    "<Yukari>.PositionOfYukarisChair ~ <2, 3>",
	    "<Yukari>.RenderGroup ~ <RenderGroup>",
	    "<Yukari>.RenderLayer ~ 0",
	    "<Yukari>.ComputedScale ~ <1, 1, 1>",
	    "<Yukari>.SortingID ~ 20",
	    "<Yukari>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
	    "<Yukari>.Visible ~ True",
	    "<VNState>.OperationID ~ 12345",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (<Yukari>, Default)",
	    "<VNState>.DialogueLog ~ Nobody:12345",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:12345",
	    //Yukari's 12345 also gets skipped
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 2345)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 345)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, 45)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 4 }, 5)",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 5 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.OperationID ~ 6789",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (<Yukari>, Default)",
	    "<VNState>.DialogueLog ~ Nobody:6789",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:6789",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 6 }, 789)",
	    "<VNState>.$UpdateCount ~ 1",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 7 }, 89)",
	    "<VNState>.$UpdateCount ~ 2",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 8 }, 9)",
	    "<VNState>.$UpdateCount ~ 3",
	    "<TestDialogueBox>.Dialogue ~ (Char { fragment = 9 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<Yukari>.EntityActive ~ False",
	    "<VNState>.ContextFinished ~ StrongContext:l2",
	    "<VNState>.OperationID ~ 67890123",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (<Reimu>, Default)",
	    "<VNState>.DialogueLog ~ Reimu:67890123",
	    "<TestDialogueBox>.DialogueStarted ~ Reimu:67890123",
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
		"<VNState>.InputAllowed ~ True",
		"<VNState>.OperationID ~ $$__OPEN__$$",
		"<VNState>.RenderGroupCreated ~ <RenderGroup>",
		"<VNState>.VNStateActive ~ True",
		"<RenderGroup>.EntityActive ~ True",
		"<RenderGroup>.ComputedEulerAnglesD ~ <0, 0, 0>",
		"<RenderGroup>.ComputedLocation ~ <0, 0, 0>",
		"<RenderGroup>.NestedRenderGroup ~ ",
		"<RenderGroup>.Priority ~ 0",
		"<RenderGroup>.RenderLayer ~ 0",
		"<RenderGroup>.ComputedScale ~ <1, 1, 1>",
		"<RenderGroup>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
		"<RenderGroup>.Visible ~ True",
		"<RenderGroup>.Zoom ~ 1",
		"<RenderGroup>.ZoomTarget ~ <0, 0, 0>",
		"<RenderGroup>.ZoomTransformOffset ~ <0, 0, 0>",
		"<VNState>.ContextStarted ~ Context:l1",
		"<VNState>.OperationID ~ $$__OPEN__$$::l1",
		"<RenderGroup>.RendererAdded ~ <TestDialogueBox>",
		"<VNState>.EntityCreated ~ <TestDialogueBox>",
		"<TestDialogueBox>.EntityActive ~ True",
		"<TestDialogueBox>.ComputedEulerAnglesD ~ <0, 0, 0>",
		"<TestDialogueBox>.ComputedLocation ~ <0, 0, 0>",
		"<TestDialogueBox>.RenderGroup ~ <RenderGroup>",
		"<TestDialogueBox>.RenderLayer ~ 0",
		"<TestDialogueBox>.ComputedScale ~ <1, 1, 1>",
		"<TestDialogueBox>.SortingID ~ 0",
		"<TestDialogueBox>.Speaker ~ (, Default)",
		"<TestDialogueBox>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
		"<TestDialogueBox>.Visible ~ True",
		"<RenderGroup>.RendererAdded ~ <Reimu>",
		"<VNState>.EntityCreated ~ <Reimu>",
		"<Reimu>.Emote ~ ",
		"<Reimu>.Emotion ~ Neutral",
		"<Reimu>.EntityActive ~ True",
		"<Reimu>.ComputedEulerAnglesD ~ <0, 0, 0>",
		"<Reimu>.GoheiLength ~ 14",
		"<Reimu>.ComputedLocation ~ <0, 0, 0>",
		"<Reimu>.RenderGroup ~ <RenderGroup>",
		"<Reimu>.RenderLayer ~ 0",
		"<Reimu>.ComputedScale ~ <1, 1, 1>",
		"<Reimu>.SortingID ~ 10",
		"<Reimu>.ComputedTint ~ RGBA(1.000, 1.000, 1.000, 1.000)",
		"<Reimu>.Visible ~ True",
		"<VNState>.$UpdateCount ~ 0",
		"<VNState>.OperationID ~ 12345",
		"<TestDialogueBox>.DialogueCleared ~ ()",
		"<TestDialogueBox>.Speaker ~ (<Reimu>, Default)",
		"<VNState>.DialogueLog ~ Reimu:12345",
		"<TestDialogueBox>.DialogueStarted ~ Reimu:12345",
		//Reimu's 12345 gets skipped
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 1 }, 2345)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 2 }, 345)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 3 }, 45)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 4 }, 5)",
		"<TestDialogueBox>.Dialogue ~ (Char { fragment = 5 }, )",
		"<TestDialogueBox>.DialogueFinished ~ ()",
		//The inner context is constructed and immediately destroyed without constructing Yukari.
		//This is the major computational benefit of bounded contexts w.r.t loading/backlogging.
		"<VNState>.ContextStarted ~ StrongContext:l2",
		"<VNState>.OperationID ~ $$__OPEN__$$::l2",
		"<VNState>.ContextFinished ~ StrongContext:l2",
		"<VNState>.OperationID ~ 67890123",
		"<TestDialogueBox>.DialogueCleared ~ ()",
		"<TestDialogueBox>.Speaker ~ (<Reimu>, Default)",
		"<VNState>.DialogueLog ~ Reimu:67890123",
		"<TestDialogueBox>.DialogueStarted ~ Reimu:67890123",
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
		"<VNState>.OperationID ~ 4567",
		"<TestDialogueBox>.DialogueCleared ~ ()",
		"<TestDialogueBox>.Speaker ~ (<Reimu>, Default)",
		"<VNState>.DialogueLog ~ Reimu:4567",
		"<TestDialogueBox>.DialogueStarted ~ Reimu:4567",
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