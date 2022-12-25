using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using NUnit.Framework;

namespace Tests.BagoumLib {
public class TestTrie {
    [Test]
    public void TestTries() {
        var t = new Trie();
        Assert.IsFalse(t.Contains("hello"));
        Assert.AreEqual(null, t.FindLongestSubstring("hello"));
        t.Add("hel");
        Assert.IsFalse(t.Contains("hello"));
        Assert.AreEqual("hel", t.FindLongestSubstring("hello"));
        t.Add("hellow");
        Assert.IsFalse(t.Contains("hello"));
        Assert.AreEqual("hel", t.FindLongestSubstring("hello"));
        t.Add("hello");
        Assert.IsTrue(t.Contains("hello"));
        Assert.AreEqual("hello", t.FindLongestSubstring("hello"));
    }
    
    private static readonly Random r = new();

    [Test]
    public void FuzzyTest() {
        var includes = new HashSet<string>();
        var trie = new Trie();
        for (int ii = 0; ii < 10000; ++ii) {
            var s = r.RandString(r.Next(2, 12));
            includes.Add(s);
            trie.Add(s);
        }
        
        foreach (var s in includes)
            Assert.IsTrue(trie.Contains(s));

        for (int ii = 0; ii < 5000; ++ii) {
            var s = r.RandString(r.Next(2, 12));
            if (!includes.Contains(s))
                Assert.IsFalse(trie.Contains(s));
        }

        var includesArr = includes.ToArray();
        var deleted = new HashSet<string>();
        for (int ii = 0; ii < 5000; ++ii) {
            var toDel = includesArr[r.Next(includesArr.Length)];
            if (deleted.Contains(toDel)) continue;
            deleted.Add(toDel);
            Assert.IsTrue(trie.Contains(toDel));
            Assert.IsTrue(trie.Remove(toDel));
            Assert.IsFalse(trie.Contains(toDel));
            Assert.IsFalse(trie.Remove(toDel));
        }

        foreach (var x in includes.Except(deleted))
            Assert.IsTrue(trie.Contains(x));

        int k = 5;
    }
}
}