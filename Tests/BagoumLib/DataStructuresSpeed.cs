using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using NUnit.Framework;

namespace Tests.BagoumLib {
public class DataStructuresSpeed {
    
    [Test]
    public void SpeedTestArrayList() {
        void Compare(int keys, int itrs) {
            Console.WriteLine($"\nFor count {keys}*{itrs}:");
            var r = new Random();
            var a = new ArrayList<float>(200000);
            var l = new List<float>(200000);
            var t = new Stopwatch();
            var f = r.NextSingle();
            float total = 0;
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr) {
                a.Clear();
                for (int ii = 0; ii < keys; ++ii)
                    a.AddIn(in f);
            }
            Console.WriteLine($"A: Set: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr) {
                l.Clear();
                //var f = r.NextSingle();
                for (int ii = 0; ii < keys; ++ii)
                    l.Add(f);
            }
            Console.WriteLine($"L: Set: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
        }

        foreach (var times in new[] { 1, 1, 2, 4, 6, 8, 10, 12, 15, 20 })
            Compare(150000, times);

        Compare(6120000, 1);
    }
    
}
}