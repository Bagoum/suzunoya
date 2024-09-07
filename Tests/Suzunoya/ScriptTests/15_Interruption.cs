using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using NUnit.Framework;
using Suzunoya.ControlFlow;
using Suzunoya.Dialogue;
using Suzunoya.Entities;
using static Tests.Suzunoya.MyTestCharacter;
using static Tests.Suzunoya.ScriptTestHelpers;
using static Tests.AssertHelpers;
using static Suzunoya.Helpers;

namespace Tests.Suzunoya {

public class _15InterruptionScriptTest {
	private class _TestScript : TestScript {
		public async Task<int> Run(bool firstC) {
			var md = vn.Add(new TestDialogueBox());
			using var yukari = vn.Add(new Yukari());
			yukari.speechCfg = SpeechSettings.Default with {
				opsPerChar = (s, i) => 1,
				opsPerSecond = 1,
				rollEvent = null
			};
			if (firstC)
				await yukari.Say("12345").C;
			else
				await yukari.Say("12345");
			await yukari.Say("67890").C;
			return 24;
		}

		public BoundedContext<int> DoThisInterruptTask() => new BoundedContext<int>(vn, "interruptor", async () => {
			var y = vn.Find<Yukari>();
			await y.Say("ABCDE").C;
			return 12;
		});

	}

	private void ScriptTestInner(bool firstC, InterruptionStatus ret, string[] cmp, int interruptFrame = 3) {
		var s = new _TestScript();
		var t = s.Run(firstC);
		s.er.LoggedEvents.Clear();
		//Mimicking some usage-specific update loop
		for (int ii = 0; !t.IsCompleted; ++ii) {
			s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
			s.vn.Update(1f);
			if (ii == interruptFrame) {
				var it = s.vn.Interrupt();
				++ii;
				for (var inner = s.DoThisInterruptTask().Execute(); !inner.IsCompleted; ++ii) {
					s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
					s.vn.Update(1f);
					if (ii == interruptFrame + 7)
						s.vn.UserConfirm();
				}
				--ii;
				it.ReturnInterrupt(ret);
			}
			if (ii == interruptFrame + 17)
				s.vn.UserConfirm();
		}
		ListEq(s.er.SimpleLoggedEventStrings, cmp);
	}

    [Test]
    public void ScriptTestWithC() => ScriptTestInner(true, InterruptionStatus.Continue, stored);
    [Test]
    public void ScriptTestWithoutC() => ScriptTestInner(false, InterruptionStatus.Continue, stored);
    [Test]
    public void ScriptTestAbort() => ScriptTestInner(true, InterruptionStatus.Abort, stored2);
    
    
    private static readonly string[] stored = {
	    "<VNState>.$UpdateCount ~ 0",
	    "<VNState>.DialogueLog ~ Nobody:12345",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:12345",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 1 }, 2345)",
	    "<VNState>.$UpdateCount ~ 1",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 2 }, 345)",
	    "<VNState>.$UpdateCount ~ 2",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 3 }, 45)",
	    "<VNState>.$UpdateCount ~ 3",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 4 }, 5)",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 5 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.InterruptionStarted ~ Suzunoya.ControlFlow.VNInterruptionLayer",
	    "<VNState>.ContextStarted ~ Context:interruptor",
	    "<VNState>.OperationID ~ $$__OPEN__$$::interruptor",
	    "<VNState>.OperationID ~ ABCDE",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (<Yukari>, None)",
	    "<VNState>.$UpdateCount ~ 4",
	    "<VNState>.DialogueLog ~ Nobody:ABCDE",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:ABCDE",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = A }, BCDE)",
	    "<VNState>.$UpdateCount ~ 5",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = B }, CDE)",
	    "<VNState>.$UpdateCount ~ 6",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = C }, DE)",
	    "<VNState>.$UpdateCount ~ 7",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = D }, E)",
	    "<VNState>.$UpdateCount ~ 8",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = E }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
	    "<VNState>.$UpdateCount ~ 9",
	    "<VNState>.$UpdateCount ~ 10",
	    "<VNState>.AwaitingConfirm ~ ",
	    "<VNState>.$UpdateCount ~ 11",
	    "<VNState>.ContextFinished ~ Context:interruptor",
	    "<VNState>.InterruptionEnded ~ Suzunoya.ControlFlow.VNInterruptionLayer",
	    "<VNState>.$UpdateCount ~ 12",
	    "<VNState>.OperationID ~ 67890",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (<Yukari>, None)",
	    "<VNState>.DialogueLog ~ Nobody:67890",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:67890",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 6 }, 7890)",
	    "<VNState>.$UpdateCount ~ 13",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 7 }, 890)",
	    "<VNState>.$UpdateCount ~ 14",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 8 }, 90)",
	    "<VNState>.$UpdateCount ~ 15",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 9 }, 0)",
	    "<VNState>.$UpdateCount ~ 16",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 0 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
	    "<VNState>.$UpdateCount ~ 17",
	    "<VNState>.$UpdateCount ~ 18",
	    "<VNState>.$UpdateCount ~ 19",
	    "<VNState>.$UpdateCount ~ 20",
	    "<VNState>.AwaitingConfirm ~ ",
	    "<VNState>.$UpdateCount ~ 21",
	    "<Yukari>.EntityActive ~ Predeletion",
	    "<Yukari>.EntityActive ~ Deleted",
    };

    private static readonly string[] stored2 = {
	    "<VNState>.$UpdateCount ~ 0",
	    "<VNState>.DialogueLog ~ Nobody:12345",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:12345",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 1 }, 2345)",
	    "<VNState>.$UpdateCount ~ 1",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 2 }, 345)",
	    "<VNState>.$UpdateCount ~ 2",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 3 }, 45)",
	    "<VNState>.$UpdateCount ~ 3",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 4 }, 5)",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = 5 }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.InterruptionStarted ~ Suzunoya.ControlFlow.VNInterruptionLayer",
	    "<VNState>.ContextStarted ~ Context:interruptor",
	    "<VNState>.OperationID ~ $$__OPEN__$$::interruptor",
	    "<VNState>.OperationID ~ ABCDE",
	    "<TestDialogueBox>.DialogueCleared ~ ()",
	    "<TestDialogueBox>.Speaker ~ (<Yukari>, None)",
	    "<VNState>.$UpdateCount ~ 4",
	    "<VNState>.DialogueLog ~ Nobody:ABCDE",
	    "<TestDialogueBox>.DialogueStarted ~ Nobody:ABCDE",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = A }, BCDE)",
	    "<VNState>.$UpdateCount ~ 5",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = B }, CDE)",
	    "<VNState>.$UpdateCount ~ 6",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = C }, DE)",
	    "<VNState>.$UpdateCount ~ 7",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = D }, E)",
	    "<VNState>.$UpdateCount ~ 8",
	    "<TestDialogueBox>.Dialogue ~ (Char { Fragment = E }, )",
	    "<TestDialogueBox>.DialogueFinished ~ ()",
	    "<VNState>.AwaitingConfirm ~ Suzunoya.ControlFlow.VNState",
	    "<VNState>.$UpdateCount ~ 9",
	    "<VNState>.$UpdateCount ~ 10",
	    "<VNState>.AwaitingConfirm ~ ",
	    "<VNState>.$UpdateCount ~ 11",
	    "<VNState>.ContextFinished ~ Context:interruptor",
	    "<VNState>.InterruptionEnded ~ Suzunoya.ControlFlow.VNInterruptionLayer",
	    "<VNState>.$UpdateCount ~ 12",
	    "<Yukari>.EntityActive ~ Predeletion",
	    "<Yukari>.EntityActive ~ Deleted",
    };
}
}