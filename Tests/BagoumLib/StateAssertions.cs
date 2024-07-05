using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using NUnit.Framework;
#pragma warning disable CS1998

namespace Tests.BagoumLib {
public class StateAssertions {
    private class MyObject1 {
        public int X = 0;
    }

    private class MyObject2 {
        public string Y = "hello";
    }

    private record MyObject1Assertion<T> : IAssertion<MyObject1Assertion<T>> {
        public string? ID { get; init; }
        public int Value { get; init; }
        public MyObject1 Realized { get; private set; } = null!;
        public async Task ActualizeOnNewState() => Realized = new MyObject1 { X = Value };
        public async Task ActualizeOnNoPreceding() => Realized = new MyObject1 { X = Value + 1000 };
        //in real usage, this would do some kind of dispose. 
        public async Task DeactualizeOnEndState() => Realized.X = -Value ;
        public async Task DeactualizeOnNoSucceeding() => Realized.X = -Value - 1000;
        public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
        public async Task _Inherit(MyObject1Assertion<T> prev) {
            Realized = prev.Realized;
            Realized.X = Value;
        }
    }
    
    private record MyObject2Assertion : IAssertion<MyObject2Assertion> {
        public string? ID { get; init; }
        public string Value { get; init; }
        public MyObject2 Realized { get; private set; } = null!;
        public async Task ActualizeOnNewState() => Realized = new MyObject2 { Y = Value };
        public async Task ActualizeOnNoPreceding() => Realized = new MyObject2 { Y = Value + "www" };
        public async Task DeactualizeOnEndState() => Realized.Y = "!" + Value;
        public async Task DeactualizeOnNoSucceeding() => Realized.Y = "!" + Value + "zzz";
        public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
        public async Task _Inherit(MyObject2Assertion prev) {
            Realized = prev.Realized;
            Realized.Y = Value;
        }
    }

    [Test]
    public async Task TestBasic() {
        var s = new IdealizedState();
        var a1 = new MyObject1Assertion<int>() { Value = 13 };
        var a2 = new MyObject2Assertion() { Value = "hello" };
        s.Assert(a1);
        Assert.AreEqual(a1.Realized, null);
        
        await s.Actualize(null, ActualizeOptions.Default);
        var r1 = a1.Realized;
        Assert.AreEqual(r1.X, 13);
        var s2 = new IdealizedState(a2, a1 with { Value = 17 });
        Assert.AreEqual(a2.Realized, null);
        await s2.Actualize(s, ActualizeOptions.Default);
        var r2 = a2.Realized;
        Assert.AreEqual(r1.X, 17);
        //actualize on no precede
        Assert.AreEqual(r2.Y, "hellowww");

        var s3 = new IdealizedState(a2 with { Value = "world"} );
        await s3.Actualize(s2, ActualizeOptions.Default);
        //deactualize on no succeed
        Assert.AreEqual(r1.X, -1017);
        Assert.AreEqual(r2.Y, "world");

        await s3.DeactualizeOnEndState(ActualizeOptions.Default);
        //unchanged
        Assert.AreEqual(r1.X, -1017);
        //deactualize on end
        Assert.AreEqual(r2.Y, "!world");
    }
    
}
}