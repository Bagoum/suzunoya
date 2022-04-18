using System;
using BagoumLib.Cancellation;
using NUnit.Framework;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Dialogue;

namespace Tests.Suzunoya {

/// <summary>
/// Tests cases where flags are modified between load and save time.
/// </summary>
public class _14ComputeFlag {
    public class _TestScript : TestScript {
	    public _TestScript(VNState? vn = null) : base(vn) { }
	    public BoundedContext<int> Run() => new(vn, "outer", async () => {
		    using var md = vn.Add(new TestDialogueBox());
		    using var reimu = vn.Add(new Reimu());
		    var result = 100;
		    //This branch should not be entered on load.
		    //if (vn.saveData.GetData<int>("var1") == 7) { <- this would cause the branch to be entered on load
		    if (await vn.ComputeFlag(vn.GetContextValue<int>("var1") == 7, "isvar1Eq7")) {
			    await vn.Wait(9999);
			    result = 200;
		    }
		    vn.SaveContextValue("var1", 7);
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
        s.vn.SaveContextValue("var1", 6);
        Assert.AreEqual(s.vn.GetContextValue<int>("var1"), 6);
        var t = s.Run().Execute();
        for (int ii = 0; ii < 3; ++ii)
	        s.vn.Update(1f);
        Assert.IsFalse(t.IsCompleted);
        var sd = s.vn.UpdateInstanceData();
        //don't do it this way in practice, GetContextValue is the correct way, lmao.
        Assert.AreEqual(sd.GetData<int>(VNState.ComputeContextsValueKey("var1", 
	        Array.Empty<string>())), 7);
        Assert.AreEqual(s.vn.GetContextValue<int>("var1"), 7);
        Assert.AreEqual(s.vn.GetFlag("outer", "isvar1Eq7"), false);
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        t = s.Run().Execute();
        for (int ii = 0; ii < 100; ++ii)
	        s.vn.Update(1f);
        Assert.AreEqual(t.Result, 100);
        sd = s.vn.UpdateInstanceData();
        Assert.AreEqual(s.vn.GetContextValue<int>("var1"), 7);
        Assert.AreEqual(s.vn.GetFlag("outer", "isvar1Eq7"), false);
        
    }
}
}