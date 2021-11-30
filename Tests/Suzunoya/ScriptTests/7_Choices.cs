using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reactive;
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

public class _6ChoicesTest {
    public class _TestScript : TestScript {
        private class AskRecvr : IInterrogatorReceiver {
            private readonly _TestScript scrpt;
            public AskRecvr(_TestScript scrpt) {
                this.scrpt = scrpt;
            }

            public void OnNext<T>(IInterrogator<T> data) {
                //In a plugin workflow, we would use this event to create some kind of GameObject that could
                // parse some input and then callback to asker.AwaitingResponse.Value.
                scrpt.asker = data;
            }
        }
        public _TestScript(VNState vn) : base(vn) { }

        public IInterrogator? asker;
        
        public async Task<float> Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            await vn.Wait(0);

            await reimu.MoveTo(Vector3.One, 1);
            vn.OperationID.OnNext("A");
            vn.InterrogatorCreated.Subscribe(new AskRecvr(this));

            var choice = new ChoiceInterrogator<float>((4.2f, "hello"), (6.9f, "<color=red>world</color>"));
            er.Record(choice);
            vn.OperationID.OnNext("B");
            var f = await vn.Ask(choice, "key2", true);
            vn.OperationID.OnNext("C");
            er.LoggedEvents.OnNext(new EventRecord.LogEvent(vn, "$CHOICE1", typeof(float), f));

            await reimu.MoveTo(Vector3.Zero, 5);
            vn.OperationID.OnNext("D");

            return f;
        }

    }
    [Test]
    public void ScriptTest() {
        var sd = new InstanceData(new GlobalData());
        var s = new _TestScript(new VNState(Cancellable.Null, sd));
        var t = s.Run();
        s.er.LoggedEvents.Clear();
        //We play a few lines, then "quit"
        for (int ii = 0; ii < 7; ++ii) {
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
            if (ii == 3)
                ((ChoiceInterrogator<float>) s.asker!).AwaitingResponse.Value!(4.2f);
        }
        s.vn.UpdateSavedata();
        Assert.AreEqual(sd.Location, new VNLocation("C", new List<string>()));
        Assert.AreEqual(sd.Data["$$__ctxResult__$$::key2"], 4.2f);
        Assert.AreEqual(sd.Data["key2"], 4.2f);
        ListEq(s.er.SimpleLoggedEventStrings, stored1);
        //Then we load again
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Run();
        s.er.LoggedEvents.Clear();
        for (int ii = 0; !t.IsCompleted; ++ii) {
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
        }
        int k = 5;
        ListEq(s.er.SimpleLoggedEventStrings, stored2);
    }

    private static readonly string[] stored1 = {
        "<VNState>.$UpdateCount ~ 0",
        "<Reimu>.Location ~ <0, 0, 0>",
        "<Reimu>.Location ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 1",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<VNState>.OperationID ~ A",
        "<ChoiceInterrogator<Single>>.AwaitingResponse ~ ",
        "<ChoiceInterrogator<Single>>.EntityActive ~ True",
        "<VNState>.OperationID ~ B",
        "<VNState>.OperationID ~ $$__OPEN__$$::key2",
        "<VNState>.ContextStarted ~ Context:key2",
        "<VNState>.Interrogator ~ Suzunoya.ControlFlow.ChoiceInterrogator`1[System.Single]",
        "<ChoiceInterrogator<Single>>.AwaitingResponse ~ System.Action`1[System.Single]",
        "<VNState>.$UpdateCount ~ 2",
        "<VNState>.$UpdateCount ~ 3",
        "<ChoiceInterrogator<Single>>.AwaitingResponse ~ ",
        "<ChoiceInterrogator<Single>>.EntityActive ~ False",
        "<VNState>.ContextFinished ~ Context:key2",
        "<VNState>.OperationID ~ C",
        "<VNState>.$CHOICE1 ~ 4.2",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.Location ~ <0.9045085, 0.9045085, 0.9045085>",
        "<VNState>.$UpdateCount ~ 6",
        "<Reimu>.Location ~ <0.6545085, 0.6545085, 0.6545085>"
    };
    
    
    private static readonly string[] stored2 = {
        "<VNState>.$UpdateCount ~ 0",
        "<Reimu>.Location ~ <0, 0, 0>",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<VNState>.OperationID ~ A",
        //The VN does not send an InterrogatorCreated event, 
        // and interrogator.AwaitingResponse is never changed from null.
        // The interrogator is created, but that's fine-- it's just data.
        "<ChoiceInterrogator<Single>>.AwaitingResponse ~ ",
        "<ChoiceInterrogator<Single>>.EntityActive ~ True",
        "<VNState>.OperationID ~ B",
        "<VNState>.OperationID ~ $$__OPEN__$$::key2",
        "<VNState>.ContextStarted ~ Context:key2",
        "<VNState>.ContextFinished ~ Context:key2",
        "<VNState>.OperationID ~ C",
        "<VNState>.$CHOICE1 ~ 4.2",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<Reimu>.Location ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 1",
        "<Reimu>.Location ~ <0.9045085, 0.9045085, 0.9045085>",
        "<VNState>.$UpdateCount ~ 2",
        "<Reimu>.Location ~ <0.6545085, 0.6545085, 0.6545085>",
        "<VNState>.$UpdateCount ~ 3",
        "<Reimu>.Location ~ <0.3454914, 0.3454914, 0.3454914>",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.Location ~ <0.09549147, 0.09549147, 0.09549147>",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.Location ~ <0, 0, 0>",
        "<VNState>.OperationID ~ D"
    };
}
}