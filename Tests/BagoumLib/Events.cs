﻿using System;
using System.Collections.Generic;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using NUnit.Framework;
using static Tests.AssertHelpers;

namespace Tests.BagoumLib {
public class Events {
    [Test]
    public void TestAddRemove() {
        var ev = new Event<int>();
        var l1 = new List<int>();
        var l2 = new List<int>();
        var d1 = ev.Subscribe<int>(l1.Add);
        ev.OnNext(1);
        ListEq(l1, new[]{ 1 });
        ListEq(l2, new int[]{});
        var d2 = ev.Subscribe<int>(l2.Add);
        ev.OnNext(2);
        ListEq(l1, new[]{ 1, 2});
        ListEq(l2, new[]{ 2 });
        d1.Dispose();
        ev.OnNext(3);
        ListEq(l1, new[]{ 1, 2 });
        ListEq(l2, new[]{ 2, 3 });
        ev.OnCompleted();
        ev.OnNext(4);
        ListEq(l1, new[]{ 1, 2 });
        ListEq(l2, new[]{ 2, 3 });
    }

    [Test]
    public void EventedVal() {
        var ev = new Evented<int>(7);
        var l1 = new List<int>();
        var l2 = new List<int>();
        var d1 = ev.Subscribe(l1.Add);
        ev.OnNext(1);
        ListEq(l1, new[]{ 7, 1 });
        ListEq(l2, new int[]{});
        var d2 = ev.Subscribe(l2.Add);
        ev.OnNext(2);
        ListEq(l1, new[]{ 7, 1, 2});
        ListEq(l2, new[]{ 1, 2 });
        d1.Dispose();
        ev.Value = 3;
        ListEq(l1, new[]{ 7, 1, 2 });
        ListEq(l2, new[]{ 1, 2, 3 });
        ev.OnCompleted();
        ev.OnNext(4);
        ListEq(l1, new[]{ 7, 1, 2 });
        ListEq(l2, new[]{ 1, 2, 3 });
    }

    [Test]
    public void ReplaySubject() {
        var rs = new ReplayEvent<int>(2);
        var l1 = new List<int>();
        var l2 = new List<int>();
        var l3 = new List<int>();
        var d = rs.Subscribe(l1.Add);
        rs.OnNext(1);
        rs.OnNext(2);
        rs.Subscribe(l2.Add);
        rs.OnNext(3);
        rs.OnNext(4);
        rs.Subscribe(l3.Add);
        d.Dispose();
        rs.OnNext(5);
        ListEq(l1, new[]{1,2,3,4});
        ListEq(l2, new[]{1,2,3,4,5});
        ListEq(l3,new[]{3,4,5});
    }

    [Test]
    public void TestDisturbed() {
        var x1 = new Evented<float>(1);
        var x2 = new Evented<float>(2);
        var x3 = new Evented<float>(3);
        var add = new DisturbedSum<float>(10);
        var mul = new DisturbedProduct<float>(10);
        var ladd = new List<float>();
        var lmul = new List<float>();
        add.Subscribe(ladd.Add);
        mul.Subscribe(lmul.Add);
        var ta1 = add.AddDisturbance(x1);
        var ta2 = add.AddDisturbance(x2);
        x1.Value = 10;
        var ta3 = add.AddDisturbance(x3);
        var tm1 = mul.AddDisturbance(x1);
        var tm2 = mul.AddDisturbance(x2);
        var tm3 = mul.AddDisturbance(x3);
        ListEq(ladd, new float[]{10, 11, 13, 22, 25});
        ListEq(lmul, new float[]{10, 100, 200, 600});
        ladd.Clear();
        lmul.Clear();
        x2.Value = 20;
        ta2.Dispose();
        x2.Value = -2;
        tm2.Dispose();
        //Dispose actually causes recalculation, so it goes back to 23
        ListEq(ladd, new float[]{43, 23});
        ListEq(lmul, new float[]{6000, -600, 300});
    }

    [Test]
    public void TestPushLerpF() {
        var pl = new PushLerperF<float>(2, BMath.Lerp);
        var vals = new List<float>();
        pl.Subscribe(vals.Add); //0
        pl.Push(t => 10 + t); //10
        pl.Update(1); //11
        pl.Push(t => 100 + t); //11, 100 -> 11
        pl.Update(1); // 12, 101 -> 56.5
        pl.Update(1); //13, 102 -> 102
        pl.Update(1); //14, 103 -> 103
        pl.Push(t => 1000 + t); //103, 1000 -> 103
        pl.Update(1); //104, 1001 -> 552.5
        pl.Push(t => 10000 + t); //552.5, 10000 -> 552.5
        pl.Update(1); //552.5, 10001 -> 5276.75
        ListEq(vals, new float[] {
            0, 10, 11, 11, 56.5f, 102, 103, 103, 552.5f, 552.5f, 5276.75f
            
        });
    }
    
}
}