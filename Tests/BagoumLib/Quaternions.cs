using System;
using System.Numerics;
using BagoumLib;
using BagoumLib.Mathematics;
using NUnit.Framework;

namespace Tests.BagoumLib {
public class Quaternions {
    [Test]
    public void TestQuaternions() {
        var q1 = Vector3.UnitZ.RotateAroundD(40);
        var q2 = Vector3.UnitZ.RotateAroundD(80);
        var loc1 = new Vector3(1, 0, 1);
        var loc1q = new Quaternion(loc1, 0);
        var prod = q1 * loc1q * Quaternion.Conjugate(q1);
        Assert.AreEqual(prod.W, 0);
        Assert.IsTrue(prod == new Quaternion(loc1.Rotate(q1), 0));
        Assert.AreEqual(prod.Z, 1, 0.000001f);
        Assert.AreEqual(prod.X, MathF.Cos(40 * BMath.degRad));
        Assert.AreEqual(prod.Y, MathF.Sin(40 * BMath.degRad));
        
        //75% of 40->80 is +30
        Assert.IsTrue(BMath.Slerp(q1, q2, 0.75f).EqualsByDot(Vector3.UnitZ.RotateAroundD(70)));
        
        //The negative of a quaternion is equivalent to inverting the rotation axis and rotating (180-theta) degrees.
        // If the quaternion is half-angled, this effectively makes the quaternion rotate to the same point in the opposite direction.
        //75% of 40->80 in the reverse direction is -240
        Assert.IsTrue(BMath.Slerp(q1, -q2, 0.75f).EqualsByDot(Vector3.UnitZ.RotateAroundD(-200)));

        Assert.AreEqual(new Vector3(0, 0, 40).ToQuaternionD(), Vector3.UnitZ.RotateAroundD(40));
        Assert.AreEqual(new Vector3(0, 40, 0).ToQuaternionD(), Vector3.UnitY.RotateAroundD(40));
        Assert.AreEqual(new Vector3(40, 0, 0).ToQuaternionD(), Vector3.UnitX.RotateAroundD(40));
        int k = 5;
    }

    [Test]
    public void TestSlerp() {
        Assert.IsTrue(BMath.Slerp(300f.RotateAroundZ(), 200f.RotateAroundZ(), 0.25f).EqualsByDot(275f.RotateAroundZ()));
        Assert.IsTrue(BMath.Slerp(300f.RotateAroundZ(), 400f.RotateAroundZ(), 0.25f).EqualsByDot(325f.RotateAroundZ()));
        Assert.IsTrue(BMath.Slerp(300f.RotateAroundZ(), 40f.RotateAroundZ(), 0.25f).EqualsByDot(235f.RotateAroundZ()));
        //Quaternions are periodic around 2pi
        Assert.IsTrue(BMath.Slerp(300f.RotateAroundZ(), (400f+720f).RotateAroundZ(), 0.25f).EqualsByDot(325f.RotateAroundZ()));
        Assert.IsTrue(BMath.Slerp(300f.RotateAroundZ(), (40f+720f).RotateAroundZ(), 0.25f).EqualsByDot(235f.RotateAroundZ()));
        
        
        Assert.IsTrue(BMath.Slerp(100f.RotateAroundZ(), 0f.RotateAroundZ(), 0.25f).EqualsByDot(75f.RotateAroundZ()));
        Assert.IsTrue(BMath.Slerp(100f.RotateAroundZ(), 200f.RotateAroundZ(), 0.25f).EqualsByDot(125f.RotateAroundZ()));
        Assert.IsTrue(BMath.Slerp(100f.RotateAroundZ(), 40f.RotateAroundZ(), 0.25f).EqualsByDot(85f.RotateAroundZ()));
        //Quaternions are periodic around 2pi
        Assert.IsTrue(BMath.Slerp(100f.RotateAroundZ(), (200f+720f).RotateAroundZ(), 0.25f).EqualsByDot(125f.RotateAroundZ()));
        Assert.IsTrue(BMath.Slerp(100f.RotateAroundZ(), (40f+720f).RotateAroundZ(), 0.25f).EqualsByDot(85f.RotateAroundZ()));
        
    }
}
}