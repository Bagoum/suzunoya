using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tweening;
using NUnit.Framework;
using static Tests.AssertHelpers;

namespace Tests.BagoumLib {
public class TestTween {
    [SetUp]
    public void Setup() {
        Tween.DefaultDeltaTimeProvider = () => 1;
    }

    private static Task TestSteps<T>(ITweener t, Coroutines? cors, Func<T> value, T[] expected, Action<T, T> assertEq, bool lastIsComplete = true) {
        cors ??= new Coroutines();
        var tw = t.Run(cors);
        for (int ii = 0; ii < expected.Length; ++ii) {
            cors.Step();
            if (lastIsComplete && ii == expected.Length - 1)
                Assert.IsTrue(tw.IsCompleted);
            else
                Assert.IsFalse(tw.IsCompleted);
            assertEq(value(), expected[ii]);
        }
        return tw;
    }
    
    [Test]
    public void TestBasic() {
        var v = new Vector2(-2, -2);
        TestSteps(Tween.TweenTo(Vector2.Zero, Vector2.One, 2, x => v = x, Easers.ELinear, Cancellable.Null), 
            null, () => v, new [] {
            Vector2.Zero, 
            new Vector2(0.5f, 0.5f), 
            Vector2.One,
        }, VecEq);
    }

    [Test]
    public void TestReverse() {
        var v = new Vector2(-2, -2);
        TestSteps(Tween.TweenTo(Vector2.Zero, Vector2.One, 2, x => v = x, Easers.ELinear, Cancellable.Null).Reverse(), 
            null, () => v, new [] {
            Vector2.One,
            new Vector2(0.5f, 0.5f), 
            Vector2.Zero, 
        }, VecEq);
    }

    [Test]
    public void TestCancel() {
        var v = new Vector2(-2, -2);
        var cors = new Coroutines();
        var ct = new Cancellable();
        var tw = (Tween.TweenTo(Vector2.Zero, Vector2.One, 20, x => v = x, Easers.ELinear, ct) with {
                SetFinalOnCancel = true
        }).Run(cors);
        cors.Step();
        VecEq(v, Vector2.Zero);
        ct.Cancel();
        cors.Step();
        VecEq(v, Vector2.One);
        Assert.IsTrue(tw.IsCanceled);
        
        ct = new Cancellable();
        tw = (Tween.TweenTo(Vector2.Zero, Vector2.One, 20, x => v = x, Easers.ELinear, ct) with {
            SetFinalOnCancel = false
        }).Run(cors);
        cors.Step();
        VecEq(v, Vector2.Zero);
        ct.Cancel();
        cors.Step();
        VecEq(v, Vector2.Zero);
        Assert.IsTrue(tw.IsCanceled);
    }
    
    [Test]
    public void TestLoopCancel() {
        var v = new Vector2(-2, -2);
        var cors = new Coroutines();
        var ct = new Cancellable();
        var t0 = Tween.TweenTo(Vector2.Zero, Vector2.One, 2, x => v = x, Easers.ELinear, ct);
        var tw = TestSteps(t0.Then(t0.Reverse()).Loop(), cors, () => v, new[] {
            Vector2.Zero, 
            Vector2.One * 0.5f,
            Vector2.One, 
            Vector2.One * 0.5f,
            Vector2.Zero, 
            Vector2.One * 0.5f,
            Vector2.One, 
            Vector2.One * 0.5f,
            Vector2.Zero, 
        }, VecEq, false);
        ct.Cancel();
        //Cancellation requires one step before it is processed
        Assert.IsFalse(tw.IsCanceled);
        cors.Step();
        Assert.IsTrue(tw.IsCanceled);
    }

    [Test]
    public void TestEase() {
        var v = new Vector2(-2, -2);
        TestSteps(Tween.TweenTo(Vector2.Zero, Vector2.One, 2, x => v = x, Easers.EInSine, Cancellable.Null), 
            null, () => v, new [] {
            Vector2.Zero, 
            Vector2.One * Easers.EInSine(0.5f),
            Vector2.One,
        }, VecEq);
    }
}
}