using System.Numerics;
using System.Reactive;
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

public class _2TweeningSkipCancelScriptTest {
    /// <summary>
    /// Tests tweening, with cancellation usage.
    /// </summary>
    private class _TestScript : TestScript {
        public void Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
            reimu.LocalLocation.Value = Vector3.Zero;
            var t = reimu.MoveTo(Vector3.One, 1f, Easers.ELinear).Task;
            er.GetAndClear();
            //First update dT not counted
            vn.Update(200f);
            ListEq(er.GetAndClear(), new EventRecord.LogEvent[] {
                //not published since location is unchanged
                //new(reimu, "ComputedLocation", Vector3.Zero)
            });
            vn.Update(0.5f);
            ListEq(er.GetAndClear(), new EventRecord.LogEvent[] {
                new(reimu, "ComputedLocation", 0.5f * Vector3.One)
            });
            vn.Update(0.5f);
            ListEq(er.GetAndClear(), new EventRecord.LogEvent[] {
                new(reimu, "ComputedLocation", Vector3.One)
            });
            Assert.IsTrue(t.IsCompletedSuccessfully);
            t = reimu.MoveTo(Vector3.Zero, 100f, Easers.ELinear).Task;
            vn.SkipOperation();
            ListEq(er.GetAndClear(), new EventRecord.LogEvent[] {
                //No immediate change
                //new(reimu, "ComputedLocation", Vector3.One)
            });
            //Skips take effect on next step
            vn.Update(0.01f);
            ListEq(er.GetAndClear(), new EventRecord.LogEvent[] {
                new(reimu, "ComputedLocation", Vector3.Zero)
            });
            Assert.IsTrue(t.IsCompletedSuccessfully);
            
            //Since the task is completed, the operation is closed, and this task will open a new operation
            t = reimu.MoveTo(Vector3.One, 100f).Task;
            vn.SkipOperation();
            //The task is not yet closed, so the operation remains open even if under a skip,
            // so this task is batched under the same operation
            var t2 = reimu.RotateTo(Vector3.One, 100f).Task;
            ListEq(er.GetAndClear(), new EventRecord.LogEvent[] {
                //new(reimu, "Location", Vector3.Zero),
                //new(reimu, "EulerAnglesD", Vector3.Zero)
            });
            vn.Update(0.01f);
            //Both tasks are immediately sent to the ending point.
            ListEq(er.GetAndClear(), new EventRecord.LogEvent[] {
                new(reimu, "ComputedLocation", Vector3.One),
                new(reimu, "ComputedEulerAnglesD", Vector3.One)
            });
            Assert.IsTrue(t.IsCompletedSuccessfully && t2.IsCompletedSuccessfully);
            
            //Now let's do it with a hard cancel.
            t = reimu.MoveTo(Vector3.Zero, 2f).Task;
            vn.Update(1f);
            vn.DeleteAll();
            Assert.Throws<DestroyedObjectException>(() => reimu.RotateTo(Vector3.Zero, 2f));
            ListEq(er.GetAndClear(), new EventRecord.LogEvent[] {
                //new(reimu, "Location", Vector3.One),
                //new(reimu, "Location", Vector3.One),
                new(vn.DefaultRenderGroup, "EntityActive", EntityState.Predeletion),
                new(md, "EntityActive", EntityState.Predeletion),
                new(reimu, "EntityActive", EntityState.Predeletion),
                new(md, "EntityActive", EntityState.Deleted),
                new(reimu, "EntityActive", EntityState.Deleted),
                new(vn.DefaultRenderGroup, "EntityActive", EntityState.Deleted),
                new(vn, "VNStateActive", false)
                //The rotation function does not start due to cancellation
            });
            Assert.Throws<DestroyedObjectException>(() => vn.Update(1f));
            //No changes made due to cancellation
            Assert.AreEqual(er.LoggedEvents.Published.Count, 0);
            Assert.IsTrue(t.IsCanceled);

        }

    }
    [Test]
    public void ScriptTest() {
        new _TestScript().Run();
    }
}
}