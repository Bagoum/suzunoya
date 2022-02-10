using System.Numerics;
using BagoumLib.Cancellation;
using NUnit.Framework;
using Suzunoya.ControlFlow;
using Suzunoya.Data;

namespace Tests.Suzunoya {
public class ParentingTests {
    [Test]
    public void ParentingTest1() {
        var vn = new VNState(Cancellable.Null, new InstanceData(new GlobalData()));
        var parent = vn.Add(new Reimu());
        parent.Location.Value = new(2, 3, 0);
        var child = vn.Add(new Yukari());
        child.Location.Value = new(1, 6, 0);
        child.Parent = parent;
        Assert.AreEqual(child.ComputedLocation.Value, new Vector3(3, 9, 0));
        parent.Location.Value = new(3, 2, 0);
        Assert.AreEqual(child.ComputedLocation.Value, new Vector3(4, 8, 0));
        child.Parent = null;
        Assert.AreEqual(child.ComputedLocation.Value, new Vector3(1, 6, 0));
        child.Parent = parent;
        child.Location.Value = new(2, 5, 0);
        Assert.AreEqual(child.ComputedLocation.Value, new Vector3(5, 7, 0));
        parent.Delete();
        Assert.AreEqual(child.EntityActive.Value, false);
    }
}
}