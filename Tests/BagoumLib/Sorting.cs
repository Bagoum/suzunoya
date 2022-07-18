// Adapted from https://github.com/BonzaiThePenguin/WikiSort/blob/master/WikiSort.java

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.Sorting;
using NUnit.Framework;

namespace Tests.BagoumLib {

// class to test stable sorting (index will contain its original index in the array, to make sure it doesn't switch places with other items)
struct Test {
    public int value;
    public int index;
    public Vector4 extraData1;
    public Vector4 extraData2;
    public Vector4 extraData3;
    public Vector4 extraData4;
    public Vector4 extraData5;
}

class TestComparator: IComparer<Test> {
    public static int comparisons = 0;
    public int Compare(Test a, Test b) {
        comparisons++;
        if (a.value < b.value) return -1;
        if (a.value > b.value) return 1;
        return 0;
    }

    public LeqCompare<Test> Comp = (in Test a, in Test b) => {
	    comparisons++;
	    return (a.value <= b.value);
    };
}

class SortRandom {
    public static Random rand = new Random();
    public static int nextInt(int max) {
        return rand.Next(max);
    }
    public static int nextInt() {
        return nextInt(2147483647);
    }
}

class Testing {
    public virtual int value(int index, int total) {
        return index;
    }
}
class TestingRandom : Testing {
    public override int value(int index, int total) {
        return SortRandom.nextInt();
    }
}
class TestingRandomFew : Testing {
    public override int value(int index, int total) {
        return SortRandom.nextInt(100);
    }
}
class TestingMostlyDescending : Testing {
    public override int value(int index, int total) {
        return total - index + SortRandom.nextInt(5) - 2;
    }
}

class TestingMostlyAscending : Testing {
    public override int value(int index, int total) {
        return index + SortRandom.nextInt(5) - 2;
    }
}
class TestingPercentageMostlyAscending : Testing {
	private readonly float pct;

	public TestingPercentageMostlyAscending(float Percent) {
		this.pct = Percent;
	}
	public override int value(int index, int total) {
		if (SortRandom.rand.NextDouble() < pct)
			return index + SortRandom.nextInt(5) - 2;
		else
			return SortRandom.nextInt();
	}
}
class TestingAscending : Testing {
    public override int value(int index, int total) {
        return index;
    }
}
class TestingDescending : Testing {
    public override int value(int index, int total) {
        return total - index;
    }
}
class TestingEqual : Testing {
    public override int value(int index, int total) {
        return 1000;
    }
}
class TestingJittered : Testing {
    public override int value(int index, int total) {
        return (SortRandom.nextInt(100) <= 90) ? index : (index - 2);
    }
}
class TestingMostlyEqual : Testing {
    public override int value(int index, int total) {
        return 1000 + SortRandom.nextInt(4);
    }
}
// the last 1/5 of the data is random
class TestingAppend : Testing {
    public override int value(int index, int total) {
        if (index > total - total/5) return SortRandom.nextInt(total);
        return index;
    }
}

public class SortingTests {
	// make sure the items within the given range are in a stable order
    // if you want to test the correctness of any changes you make to the main WikiSort function,
    // call it from within WikiSort after each step
    static void Verify(Test[] array, int start, int end, TestComparator comp, string msg) {
        for (int index = start + 1; index < end; index++) {
            // if it's in ascending order then we're good
            // if both values are equal, we need to make sure the index values are ascending
            if (!(comp.Compare(array[index - 1], array[index]) < 0 ||
                  (comp.Compare(array[index], array[index - 1]) == 0 && array[index].index > array[index - 1].index))) {
	            Console.WriteLine("failed with message: " + msg);
                throw new Exception(msg);
            }
        }
    }

    [Test]
    public void SortTestBenchmark() {
		int testSize = 16;
		var sizes = new int[]{ 10000, 49999, 262145, 500000, 1000000 };
		TestComparator comp = new TestComparator();
		
		Testing[] correctnessCases = {
			new TestingRandom(),
			new TestingRandomFew(),
			new TestingMostlyDescending(),
			new TestingMostlyAscending(),
			new TestingPercentageMostlyAscending(0.8f),
			new TestingAscending(),
			new TestingDescending(),
			new TestingEqual(),
			new TestingJittered(),
			new TestingMostlyEqual(),
			new TestingAppend()
		};
		Testing[] profileCases = {
			new TestingRandom(),
			new TestingAscending(),
			new TestingMostlyAscending(),
			new TestingPercentageMostlyAscending(0.95f),
			new TestingMostlyDescending()
		};

		var sorters = new ISorter<Test>[] {
			new CombMergeSorter<Test>(),
			new AlternatingMergeSorter<Test>(),
			new MergeSorter<Test>(),
			//new InsertionSort<Test>(),
			//new WikiSorter<Test>()
		};
		
		Console.WriteLine("Running test cases...");
		var array = new Test[testSize];
		
		foreach (var tcase in correctnessCases) {
			foreach (var sorter in sorters) {
				for (int index = 0; index < testSize; index++) {
					array[index] = new Test {
						value = tcase.value(index, testSize),
						index = index
					};
				}
				sorter.Sort(array, 0, testSize, comp.Comp);
				Verify(array, 0, testSize, comp, $"test case {tcase.GetType().RName()} failed)");
			}
		}
		Console.WriteLine("Passed!");

		var sw = new Stopwatch();
		foreach (var pcase in profileCases) {
			Console.WriteLine($"\nPerformance for case {pcase.GetType().RName()}:");
			foreach (var total in sizes) {
				Console.WriteLine($"\tPerformance for array size {total}:");
				array = new Test[total];
				foreach (var sorter in sorters) {
					for (int index = 0; index < total; index++) {
						array[index] = new Test {
							value = pcase.value(index, total),
							index = index
						};
					}
					TestComparator.comparisons = 0;
					sw.Reset();
					sw.Start();
					sorter.Sort(array, 0, total, comp.Comp);
					sw.Stop();
					Console.WriteLine($"\t\t{sorter.GetType().RName()}: " +
					                  $"{TestComparator.comparisons} comparisons; {sw.Elapsed.TotalMilliseconds:000.0} ms");
					Verify(array, 0, total, comp, "testing the final array");
				}
			}
		}
    }
}
}