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
    public record Interrogator(VNState vn) : IChoiceAsker {
        private TaskCompletionSource<int>? currentChoice;

        public void SubmitSelection(int index) => currentChoice?.SetResult(index);

        //In an engine-specific plugin, this class might be a game object of some sort
        // that renders the options to screen while awaiting currentChoice.
        public BoundedContext<T> Ask<T>(string key, params (T value, string description)[] options)
            => new(vn, key, async () => {
                currentChoice = new TaskCompletionSource<int>();
                //Do rendering or whatever here
                return options[await currentChoice.Task].value;
            });
    }
    public class _TestScript : TestScript {
        public readonly Interrogator asker;

        public _TestScript(VNState vn) : base(vn) {
            asker = new(vn);
        }

        public BoundedContext<float> Run() => new(vn, "outer", async () => {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            await vn.Wait(0);

            await reimu.MoveTo(Vector3.One, 1);
            vn.OperationID.OnNext("A");
            var f = await asker.Ask("key2", (4.2f, "hello"), (6.9f, "<color=red>world</color>"));
            vn.OperationID.OnNext("C");
            er.LoggedEvents.OnNext(new EventRecord.LogEvent(vn, "$CHOICE1", typeof(float), f));

            await reimu.MoveTo(Vector3.Zero, 5);
            vn.OperationID.OnNext("D");

            return f;
        });

    }
    [Test]
    public void ScriptTest() {
        var sd = new InstanceData(new GlobalData());
        var s = new _TestScript(new VNState(Cancellable.Null, sd));
        var t = s.Run().Execute();
        s.er.LoggedEvents.Clear();
        for (int ii = 0; ii < 7; ++ii) {
            s.er.LoggedEvents.OnNext(s.UpdateLog(ii));
            s.vn.Update(1f);
            if (ii == 3)
                s.asker.SubmitSelection(0);
        }
        s.vn.UpdateInstanceData();
        Assert.AreEqual(sd.Location, new VNLocation("C", new List<string>{"outer"}));
        Assert.AreEqual(sd.Data["$$__ctxResult__$$::outer::key2"], 4.2f);
        ListEq(s.er.SimpleLoggedEventStrings, stored1);
        //Then we load again
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Run().Execute();
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
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.$UpdateCount ~ 1",
        "<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.OperationID ~ A",
        "<VNState>.OperationID ~ $$__OPEN__$$::key2",
        "<VNState>.ContextStarted ~ Context:key2",
        "<VNState>.$UpdateCount ~ 2",
        "<VNState>.$UpdateCount ~ 3",
        "<VNState>.ContextFinished ~ Context:key2",
        "<VNState>.OperationID ~ C",
        "<VNState>.$CHOICE1 ~ 4.2",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 4",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.ComputedLocation ~ <0.9045085, 0.9045085, 0.9045085>",
        "<VNState>.$UpdateCount ~ 6",
        "<Reimu>.ComputedLocation ~ <0.6545085, 0.6545085, 0.6545085>",
    };
    
    
    private static readonly string[] stored2 = {
        "<VNState>.$UpdateCount ~ 0",
        //"<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.OperationID ~ A",
        "<VNState>.OperationID ~ $$__OPEN__$$::key2",
        "<VNState>.ContextStarted ~ Context:key2",
        "<VNState>.ContextFinished ~ Context:key2",
        "<VNState>.OperationID ~ C",
        "<VNState>.$CHOICE1 ~ 4.2",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        //"<Reimu>.ComputedLocation ~ <1, 1, 1>",
        "<VNState>.$UpdateCount ~ 1",
        "<Reimu>.ComputedLocation ~ <0.9045085, 0.9045085, 0.9045085>",
        "<VNState>.$UpdateCount ~ 2",
        "<Reimu>.ComputedLocation ~ <0.6545085, 0.6545085, 0.6545085>",
        "<VNState>.$UpdateCount ~ 3",
        "<Reimu>.ComputedLocation ~ <0.3454914, 0.3454914, 0.3454914>",
        "<VNState>.$UpdateCount ~ 4",
        "<Reimu>.ComputedLocation ~ <0.09549147, 0.09549147, 0.09549147>",
        "<VNState>.$UpdateCount ~ 5",
        "<Reimu>.ComputedLocation ~ <0, 0, 0>",
        "<VNState>.OperationID ~ D",
        "<VNState>.ContextFinished ~ Context:outer",
    };
}
}