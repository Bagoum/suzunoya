using System;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using NUnit.Framework;

namespace Tests.BagoumLib {
public class Lerpers {
    [Test]
    public void LerpTest() {
        var x = new PushLerper<float>(10, BMath.LerpU);
        x.Push(4);
        Assert.AreEqual(x.Value, 4);
        x.Push(18);
        x.Update(5);
        Assert.AreEqual(x.Value, 11);
        x.Update(7);
        Assert.AreEqual(x.Value, 18);
    }
}
}