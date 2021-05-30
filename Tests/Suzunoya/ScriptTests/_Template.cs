using System.Numerics;
using System.Reactive;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using NUnit.Framework;
using Suzunoya.Entities;
using static Tests.Suzunoya.MyTestCharacter;
using static Tests.Suzunoya.ScriptTestHelpers;
using static Tests.AssertHelpers;
using static Suzunoya.Helpers;

namespace Tests.Suzunoya {

public class TemplateTest {
    /// <summary>
    /// Tests tweening, with cancellation usage.
    /// </summary>
    public class _TestScript : TestScript {
        public void Run() {
            var md = vn.Add(new TestDialogueBox());
            var reimu = vn.Add(new Reimu());
        }

    }
    [Test]
    public void ScriptTest() {
        new _TestScript().Run();
    }
}
}