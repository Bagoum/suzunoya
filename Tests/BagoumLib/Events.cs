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
        var d1 = ev.Subscribe(l1.Add);
        ev.Publish(1);
        ListEq(l1, new[]{ 1 });
        ListEq(l2, new int[]{});
        var d2 = ev.Subscribe(l2.Add);
        ev.Publish(2);
        ListEq(l1, new[]{ 1, 2});
        ListEq(l2, new[]{ 2 });
        d1.Dispose();
        ev.Publish(3);
        ListEq(l1, new[]{ 1, 2 });
        ListEq(l2, new[]{ 2, 3 });
        ev.OnCompleted();
        ev.Publish(4);
        ListEq(l1, new[]{ 1, 2 });
        ListEq(l2, new[]{ 2, 3 });
    }

    [Test]
    public void EventedVal() {
        var ev = new Evented<int>(7);
        var l1 = new List<int>();
        var l2 = new List<int>();
        var d1 = ev.Subscribe(l1.Add);
        ev.Publish(1);
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
        ev.Publish(4);
        ListEq(l1, new[]{ 7, 1, 2 });
        ListEq(l2, new[]{ 1, 2, 3 });
    }
}
}