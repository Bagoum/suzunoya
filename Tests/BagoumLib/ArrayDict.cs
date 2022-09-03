using System;
using System.Collections.Generic;
using System.Diagnostics;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using NUnit.Framework;

namespace Tests.BagoumLib {
public class ArrayDict {
    [Test]
    public void SpeedTest() {

        float Compare(int keys, int itrs) {
            Console.WriteLine($"\nFor count {keys}:");
            var r = new Random();
            var a = new ArrayDictionary<float>();
            var d = new Dictionary<int, float>();
            var t = new Stopwatch();
            float total = 0;
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr)
            for (int ii = 0; ii < keys; ++ii)
                d[ii] = r.NextSingle();
            Console.WriteLine($"D: Set0: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr)
            for (int ii = 0; ii < keys; ++ii)
                total += d[ii];
            Console.WriteLine($"D: Read: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr)
            for (int ii = 0; ii < keys; ++ii)
                d[ii] = r.NextSingle();
            Console.WriteLine($"D:  Set: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr)
                for (int ii = 0; ii < keys; ++ii)
                    a[ii] = r.NextSingle();
            Console.WriteLine($"A: Set0: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr)
                for (int ii = 0; ii < keys; ++ii)
                    total += a[ii];
            Console.WriteLine($"A: Read: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr)
                for (int ii = 0; ii < keys; ++ii)
                    a[ii] = r.NextSingle();
            Console.WriteLine($"A:  Set: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
            return total;
        }

        foreach (var times in new[] { 1, 1, 2, 4, 6, 8, 10, 12, 15, 20 })
            Compare(times, 10000);

        Compare(1, 6120000);
    }
}
}