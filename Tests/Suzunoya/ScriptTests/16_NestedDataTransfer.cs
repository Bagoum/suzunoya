using System;
using BagoumLib.Cancellation;
using NUnit.Framework;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Dialogue;

namespace Tests.Suzunoya {

/// <summary>
/// Tests proxy loading for nested data transfer.
/// </summary>
public class _16NestedDataTransfer {
    private class _TestScript : TestScript {
	    public _TestScript(VNState? vn = null) : base(vn) { }

	    public BoundedContext<int> RunC() => new(vn, "c", async () => {
		    return 1000;
	    });
	    public BoundedContext<int> RunB() => new(vn, "b", async () => {
		    return 100 + await RunC();
	    });
	    public BoundedContext<int> Run() => new(vn, "a", async () => {
		    using var md = vn.Add(new TestDialogueBox());
		    using var reimu = vn.Add(new Reimu());
		    Assert.IsFalse(vn.TryGetContextData<int>(out var _, "a", "b", "c"));
		    await RunB();
		    Assert.AreEqual(vn.GetContextResult<int>("a", "b"), 1100);
		    Assert.AreEqual(vn.GetContextResult<int>("a", "b", "c"), 1000);
		    
		    reimu.speechCfg = SpeechSettings.Default with {
			    opsPerChar = (s, i) => 1,
			    opsPerSecond = 1,
			    rollEvent = null
		    };
		    await reimu.Say("111111");
		    return 10;
	    });
    }
    [Test]
    public void ScriptTest() {
        var s = new _TestScript(new VNState(Cancellable.Null, new InstanceData(new GlobalData())));
        var t = s.Run().Execute();
        for (int ii = 0; ii < 3; ++ii)
	        s.vn.Update(1f);
        Assert.IsFalse(t.IsCompleted);
        var sd = s.vn.UpdateInstanceData();
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Run().Execute();
        for (int ii = 0; ii < 100; ++ii)
	        s.vn.Update(1f);
        Assert.AreEqual(t.Result, 10);
        sd = s.vn.UpdateInstanceData();
        int k = 5;

    }
}
}