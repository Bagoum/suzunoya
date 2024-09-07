using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;

namespace BagoumLib.DataStructures {
/// <summary>
/// A trie structure that supports finding the longest substring (starting from zero) of a string stored within it.
/// <br/>The root node represents the empty string.
/// </summary>
public class Trie {
    /// <summary>
    /// Whether or not this node marks the existence of a character.
    /// </summary>
    public bool IsLeaf { get; private set; }
    /// <summary>
    /// The parent of this trie.
    /// </summary>
    public Trie? Parent { get; }
    private readonly Dictionary<char, Trie> branches = new();

    /// <summary>
    /// Create a trie.
    /// </summary>
    public Trie() { }

    /// <summary>
    /// Create a trie from a set of strings.
    /// </summary>
    public Trie(IEnumerable<string> contents) {
        foreach (var s in contents)
            Add(s);
    }
    
    private Trie(Trie parent) {
        this.Parent = parent;
    }

    /// <summary>
    /// Add a string to this trie.
    /// <br/>If this is not the root trie, then this is equivalent to adding the string made of this trie's
    ///  stored string and the provided string. eg. If this trie represents "abc" and Add("def") is called on this trie,
    ///  then "abcdef" is added to the root trie.
    /// </summary>
    public void Add(string str, int fromIndex = 0) {
        if (fromIndex >= str.Length) {
            IsLeaf = true;
            return;
        }
        if (!branches.TryGetValue(str[fromIndex], out var nxt))
            nxt = branches[str[fromIndex]] = new(this);
        nxt.Add(str, fromIndex + 1);
    }

    /// <summary>
    /// Check whether a string exists in this trie.
    /// </summary>
    public bool Contains(string str, int fromIndex = 0) {
        if (fromIndex >= str.Length)
            return IsLeaf;
        return branches.TryGetValue(str[fromIndex], out var nxt) && nxt.Contains(str, fromIndex + 1);
    }

    /// <summary>
    /// Find the longest substring (starting at index zero) of a string that exists within this trie.
    /// Eg. If this trie contains "abc" and "ape", then
    /// <br/>FindLongestSubstring("abcd") = "abc"
    /// <br/>FindLongestSubstring("pe") = null
    /// </summary>
    public string? FindLongestSubstring(string str) {
        var current = this;
        int? latestEndIndex = null;
        for (int ii = 0;; ++ii) {
            if (current.IsLeaf)
                latestEndIndex = ii;
            if (ii >= str.Length)
                break;
            if (!current.branches.TryGetValue(str[ii], out var nxt))
                break;
            current = nxt;
        }
        return latestEndIndex.Try(out var end) ?
            new string(str.AsSpan(0, end)) :
            null;
    }

    /// <summary>
    /// Remove a string from this trie.
    /// </summary>
    /// <returns>True iff a string was removed.</returns>
    public bool Remove(ReadOnlySpan<char> str) {
        return RemoveInternal(str) > 0;
    }

    private int RemoveInternal(ReadOnlySpan<char> str) {
        //0 = no deletion occured
        //1 = deletion occured
        //2 = deletion occured and the node is now empty
        if (str.IsEmpty) {
            if (!IsLeaf) return 0;
            IsLeaf = false;
            return branches.Count == 0 ? 2 : 1;
        } else {
            if (!branches.TryGetValue(str[0], out var nxt))
                return 0;
            var nresult = nxt.RemoveInternal(str[1..]);
            if (nresult == 2) {
                branches.Remove(str[0]);
                return (!IsLeaf && branches.Count == 0) ? 2 : 1;
            }
            return nresult;
        }
    }
}
}