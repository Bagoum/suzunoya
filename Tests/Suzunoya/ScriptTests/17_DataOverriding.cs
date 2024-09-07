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
public class _17DataOverriding {
    private class _TestScript : TestScript {
	    public int NextAValue { get; set; } = 492;
	    public _TestScript(VNState? vn = null) : base(vn) { }
	    public BoundedContext<int> RunB() => new(vn, "b", async () => {
		    return 163;
	    });
	    public BoundedContext<int> Run() => new(vn, "a", async () => {
		    await vn.Wait(10);
		    vn.SaveLocalValue("hello", NextAValue);
		    return NextAValue;
	    });
    }
    [Test]
    public void ScriptTest() {
        var s = new _TestScript(new VNState(Cancellable.Null, new InstanceData(new GlobalData())));
        var t = s.Run().Execute();
        Assert.IsFalse(s.vn.TryGetContextValue("hello", out int _, "a"));
        for (int ii = 0; ii <= 10; ++ii)
	        s.vn.Update(1f);
        Assert.IsTrue(s.vn.TryGetContextValue("hello", out int x, "a") && x == 492);
        Assert.IsTrue(t.IsCompleted);
        var sd = s.vn.UpdateInstanceData();
        
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        s.NextAValue = 719;
        //Context value is still saved
        Assert.IsTrue(s.vn.TryGetContextValue("hello", out x, "a") && x == 492);
        t = (s.Run() with { OnRepeat = RepeatContextExecution.Reset }).Execute();
        //But if we run the context again, the context values are reset
        Assert.IsFalse(s.vn.TryGetContextValue("hello", out int _, "a"));
        for (int ii = 0; ii <= 10; ++ii)
	        s.vn.Update(1f);
        Assert.IsTrue(s.vn.TryGetContextValue("hello", out x, "a") && x == 719);
        Assert.IsTrue(t.IsCompleted);
        sd = s.vn.UpdateInstanceData();
        
        
        s = new _TestScript(new VNState(Cancellable.Null, sd));
        s.NextAValue = 836;
        //Context value is still saved
        Assert.IsTrue(s.vn.TryGetContextValue("hello", out x, "a") && x == 719);
        //note: reuse is default behavior
        t = (s.Run() with { OnRepeat = RepeatContextExecution.Reuse }).Execute();
        //If we run the context again, the context values are the same until overridden
        Assert.IsTrue(s.vn.TryGetContextValue("hello", out x, "a") && x == 719);
        Assert.IsTrue(s.vn.GetContextResult<int>("a") == 719);
        for (int ii = 0; ii <= 10; ++ii)
	        s.vn.Update(1f);
        Assert.IsTrue(s.vn.TryGetContextValue("hello", out x, "a") && x == 836);
        Assert.IsTrue(t.IsCompleted);
    }
}
}