using System;
using System.Numerics;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using NUnit.Framework;
using Scriptor;
using static NUnit.Framework.Assert;
using static Scriptor.Math.SimpleParser;

namespace Tests.TScriptor {
public class SimpleParserTests {
    [Test]
    public static void TestFloat() {
        AreEqual(4.4f, Float("4.4"));
        AreEqual(4.4f, Float("--4.4"));
        AreEqual(-4.4f, Float("-4.4"));
        AreEqual(-.4f * 120f, Float("+-0.4s"));
        AreEqual(-.4f * (1/120f), Float("-+.4f"));
        AreEqual(-.4f * BMath.PHI, Float("-+.4p"));
        AreEqual(-.4f * BMath.IPHI, Float("-+.4h"));
        AreEqual(-2f * BMath.PI, Float("-2.π"));
        AreEqual(2f * BMath.PI, Float("2π"));
        AreEqual(false, TryFloat("kemrlge", out _));
        AreEqual(false, TryFloat("<4", out _));
        AreEqual(false, TryFloat("4>", out _));
    }
    
    [Test]
    public static void TestV2RV2() {
        AreEqual(V2RV2.Zero, ParseV2RV2("<>"));
        AreEqual(V2RV2.Angle(2 * BMath.IPHI), ParseV2RV2("<2h>"));
        AreEqual(V2RV2.Rot(4.0f, -2.5f, 2 * BMath.IPHI), ParseV2RV2("<4.0;-2.5:2h>"));
        Throws(typeof(InvalidCastException), () => ParseV2RV2("<gf;-2.5:2h>"));
        AreEqual(new V2RV2(2f, 3f, 4.0f, -2.5f, 2 * BMath.IPHI), ParseV2RV2("<2;3:4.0;-2.5:2h>"));
        AreEqual(new V2RV2(2f, 3f, 0, 0, 2 * BMath.IPHI), ParseV2RV2("<2;3:;:2h>"));
    }
}
}