using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scriptor.Analysis;

namespace Tests.TScriptor;

public class TEFArrayPool {
    [Test]
    public void CheckArrayGetSet() {
        var q = new Queue<int[]>();
        var r = new Random();
        for (int jj = 0; jj < 10000; ++jj) {
            var rlen = r.Next(1, 25);
            var a = EFArrayPool<int>.Rent(rlen);
            Assert.GreaterOrEqual(a.Length, rlen);
            q.Enqueue(a);
            while (q.Count > 200 + r.Next(-20, 20))
                EFArrayPool<int>.Return(q.Dequeue());
        }
        
        for (int ii = 1; ii < 20; ++ii) {
            Console.WriteLine($"{ii} {EFArrayPool<int>.BucketForGetLength(ii)} {EFArrayPool<int>.BucketForSetLength(ii)}");
        }
    }
}