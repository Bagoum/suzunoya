using System;
using System.Collections.Generic;
using BagoumLib.Events;
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
}
}