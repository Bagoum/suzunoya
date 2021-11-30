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

public class _13BoundedContextDefaultTest {
    public class _TestScript : TestScript {
	    public _TestScript(VNState? vn = null) : base(vn) { }
	    public BoundedContext<int> Run() => new(vn, "outer", async () => {
		    using var md = vn.Add(new TestDialogueBox());
		    using var reimu = vn.Add(new Reimu());
		    //test setup
		    reimu.speechCfg = SpeechSettings.Default with {
			    opsPerChar = (s, i) => 1,
			    opsPerSecond = 1,
			    rollEvent = null
		    };
		    await reimu.Say("456789");
		    await reimu.Say("111111");
		    await reimu.Say("222211");
		    return 25;
	    });
	    //Simulating an update, or an if statement, coded incorrectly, because the new value can't be skipped
	    public BoundedContext<int> Runv2() => new(vn, "outer", async () => {
		    using var md = vn.Add(new TestDialogueBox());
		    using var reimu = vn.Add(new Reimu());
		    await reimu.Say("4567");
		    var w = await InnerCtxBad();
		    return 26 + w;
	    });
	    //Simulating an update, or an if statement, coded correctly, because the new value has a default
	    public BoundedContext<int> Runv3() => new(vn, "outer", async () => {
		    using var md = vn.Add(new TestDialogueBox());
		    using var reimu = vn.Add(new Reimu());
		    await reimu.Say("4567");
		    var w = await InnerCtxGood();
		    return 27 + w;
	    });

	    private BoundedContext<int> InnerCtxBad() => new(vn, "inner_bad", async () => {
		    await vn.Wait(9999);
		    return 24;
	    });

	    private BoundedContext<int> InnerCtxGood() => new(vn, "inner_good", async () => {
		    using var yukari = vn.Add(new Yukari());
		    await yukari.Say("12345");
		    return 24;
	    }) { LoadingDefault = 900 };

    }
    [Test]
    public void ScriptTest() {
        var s = new _TestScript(new VNState(Cancellable.Null, new InstanceData(new GlobalData())));
        var t = s.Run().Execute().Task;
        for (int ii = 0; ii < 13; ++ii)
	        s.vn.Update(1f);
        var sd = s.vn.UpdateSavedata();
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Runv2().Execute().Task;
        //InnerCtxBad runs even though it shouldn't
        for (int ii = 0; ii < 600; ++ii)
	        s.vn.Update(1f);
        Assert.IsTrue(t.IsFaulted);
        
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Runv3().Execute().Task;
        for (int ii = 0; ii < 2; ++ii)
	        //Still require a few updates to go through cancelled corountines
	        s.vn.Update(0.01f);
        Assert.IsTrue(t.IsCompleted);
        Assert.AreEqual(t.Result, 927);
    }
}
}