﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Transitions;
using NUnit.Framework;
using static Tests.AssertHelpers;

namespace Tests.BagoumLib {
public class TestTween {
    [SetUp]
    public void Setup() {
        TransitionHelpers.DefaultDeltaTimeProvider = () => 1;
    }

    private static Task<Completion> TestSteps<T>(ITransition t, Coroutines? cors, Func<T> value, T[] expected, Action<T, T> assertEq, bool lastIsComplete = true) {
        cors ??= new Coroutines();
        var tw = t.Run(cors, CoroutineOptions.Default);
        void DoCompare(int ii) {
            try {
                assertEq(value(), expected[ii]);
            } catch (Exception e) {
                Console.WriteLine($"Failed steps comparison at index {ii}");
                throw;
            }
        }
        DoCompare(0);
        for (int ii = 1; ii < expected.Length; ++ii) {
            cors.Step();
            DoCompare(ii);
            if (lastIsComplete && ii == expected.Length - 1)
                Assert.IsTrue(tw.IsCompleted);
            else
                Assert.IsFalse(tw.IsCompleted);
        }
        return tw;
    }
    
    [Test]
    public void TestBasic() {
        var v = new Vector2(-2, -2);
        TestSteps(TransitionHelpers.TweenTo(Vector2.Zero, Vector2.One, 2, x => v = x, Easers.ELinear, Cancellable.Null), 
            null, () => v, new [] {
            Vector2.Zero, 
            new Vector2(0.5f, 0.5f), 
            Vector2.One,
        }, VecEq);

        v = new Vector2(-2, -2);
        TestSteps(TransitionHelpers.TweenDelta(Vector2.One, Vector2.One, 2, x => v = x, Easers.ELinear, Cancellable.Null), 
            null, () => v, new [] {
                Vector2.One, 
                new Vector2(1.5f, 1.5f), 
                Vector2.One * 2,
            }, VecEq);
    }

    [Test]
    public void TestReverse() {
        var v = new Vector2(-2, -2);
        TestSteps(TransitionHelpers.TweenTo(Vector2.Zero, Vector2.One, 2, x => v = x, Easers.ELinear, Cancellable.Null).Reverse(), 
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
        var tw = (TransitionHelpers.TweenTo(Vector2.Zero, Vector2.One, 20, x => v = x, Easers.ELinear, ct))
            .Run(cors, CoroutineOptions.Default);
        VecEq(v, Vector2.Zero);
        cors.Step();
        VecEq(v, Vector2.One / 20f);
        cors.Step();
        VecEq(v, Vector2.One / 10f);
        ct.Cancel(1);
        cors.Step();
        VecEq(v, Vector2.One);
        Assert.AreEqual(tw.Result, Completion.SoftSkip);
        
        ct = new Cancellable();
        tw = (TransitionHelpers.TweenTo(Vector2.Zero, Vector2.One, 20, x => v = x, Easers.ELinear, ct))
            .Run(cors, CoroutineOptions.Default);
        VecEq(v, Vector2.Zero);
        cors.Step();
        VecEq(v, Vector2.One / 20f);
        cors.Step();
        VecEq(v, Vector2.One / 10f);
        ct.Cancel(2);
        cors.Step();
        VecEq(v, Vector2.One / 10f);
        Assert.IsTrue(tw.IsCanceled);
        
        
        ct = new Cancellable();
        var t0 = (TransitionHelpers.TweenTo(Vector2.Zero, Vector2.One, 5, x => v = x, Easers.ELinear, ct));
        tw = t0.Then(t0.Reverse()).Run(cors, CoroutineOptions.Default);
        cors.Step();
        VecEq(v, new Vector2(0.2f, 0.2f));
        cors.Step();
        ct.Cancel(1);
        cors.Step();
        VecEq(v, Vector2.Zero);
        Assert.AreEqual(tw.Result, Completion.SoftSkip);
        
        ct = new Cancellable();
        t0 = (TransitionHelpers.TweenTo(Vector2.Zero, Vector2.One, 5, x => v = x, Easers.ELinear, ct));
        tw = t0.Then(t0.Reverse()).Run(cors, CoroutineOptions.Default);
        cors.Step();
        VecEq(v, new Vector2(0.2f, 0.2f));
        cors.Step();
        ct.Cancel(2);
        cors.Step();
        VecEq(v, new Vector2(0.4f, 0.4f));
        Assert.IsTrue(tw.IsCanceled);
    }
    
    [Test]
    public void TestLoopCancel() {
        var v = new Vector2(-2, -2);
        var cors = new Coroutines();
        var ct = new Cancellable();
        var t0 = TransitionHelpers.TweenTo(Vector2.Zero, Vector2.One, 2, x => v = x, Easers.ELinear, ct);
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
        ct.Cancel(ICancellee.SoftSkipLevel);
        //Cancellation requires one step before it is processed
        Assert.IsFalse(tw.IsCanceled);
        cors.Step();
        Assert.AreEqual(tw.Result, Completion.SoftSkip);
    }

    [Test]
    public void TestEase() {
        var v = new Vector2(-2, -2);
        TestSteps(TransitionHelpers.TweenTo(Vector2.Zero, Vector2.One, 2, x => v = x, Easers.EInSine, Cancellable.Null), 
            null, () => v, new [] {
            Vector2.Zero, 
            Vector2.One * Easers.EInSine(0.5f),
            Vector2.One,
        }, VecEq);
    }
}
}