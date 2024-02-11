using System;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using NUnit.Framework;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Dialogue;

namespace Tests.Suzunoya {

/// <summary>
/// Tests proxy loading.
/// </summary>
public class _14SaveLoadTimeInconsistency {
    private class _TestScript : TestScript {
	    public _TestScript(VNState? vn = null) : base(vn) { }

	    public BoundedContext<int> InnerResult() => new(vn, "inner", async () => 9);
	    public BoundedContext<int> Run() => new(vn, "outer", async () => {
		    using var md = vn.Add(new TestDialogueBox());
		    using var reimu = vn.Add(new Reimu());
		    vn.SaveLocalValue("var1", 1000);
		    var result = 300;
		    if (vn.GetLocalValue<int>("var1") == 7) {
			    await vn.Wait(9999);
			    result = 200;
		    }
		    vn.SaveLocalValue("var1", 7);
		    if (vn.GetLocalValue<int>("var1") == 7) {
			    result = 100;
		    }
		    result += vn.InstanceData.TryGetChainedData<int>("outer", "inner")?.Result.ValueOrSNull() ?? 1;
		    await InnerResult();
		    result += vn.InstanceData.TryGetChainedData<int>("outer", "inner")?.Result.ValueOrSNull() ?? 2;
		    
		    reimu.speechCfg = SpeechSettings.Default with {
			    opsPerChar = (s, i) => 1,
			    opsPerSecond = 1,
			    rollEvent = null
		    };
		    await reimu.Say("111111");
		    return result;
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
        //Equivalent
        Assert.AreEqual(sd.TryGetChainedData<int>("outer")?.Locals.GetData<int>("var1"), 7);
        Assert.AreEqual(s.vn.GetContextValue<int>("var1", "outer"), 7);
        Assert.AreEqual(s.vn.GetContextResult<int>("outer", "inner"), 9);
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Run().Execute();
        for (int ii = 0; ii < 100; ++ii)
	        s.vn.Update(1f);
        Assert.AreEqual(t.Result, 110);
        sd = s.vn.UpdateInstanceData();
        Assert.AreEqual(s.vn.GetContextValue<int>("var1", "outer"), 7);

    }
}
}