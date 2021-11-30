using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using NUnit.Framework;

namespace Tests.BagoumLib {
public class Helpers {
    [Test]
    public void PairSuccessive() {
        AssertHelpers.IEnumEq(new int[0].PairSuccessive(0), new (int, int)[0]);
        AssertHelpers.IEnumEq(new[]{1}.PairSuccessive(0), new[]{(1,0)});
        AssertHelpers.IEnumEq(new[]{1,2}.PairSuccessive(0), new[]{(1,2), (2,0)});
        AssertHelpers.IEnumEq(new[]{1,2,3}.PairSuccessive(0), new[]{(1,2), (2,3), (3,0)});
    }
}
}