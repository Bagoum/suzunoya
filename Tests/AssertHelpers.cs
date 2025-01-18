using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Tests {
public static class AssertHelpers {
    public static bool AssertStringEq(string expect, object obj) {
        Assert.AreEqual(expect, obj.ToString());
        return true;
    }
    public static void StringsApproxEqual(string expected, string observed) {
        expected = expected.Trim().Replace("\r", "");
        observed = observed.Trim().Replace("\r", "");
        if (expected != observed) {
            Console.WriteLine($"Expected:\n~~~\n{expected}\n~~~\nbut observed:\n~~~\n{observed}");
        }
        Assert.AreEqual(expected, observed);
    }
    public static void ListEq<T>(IReadOnlyList<T> left, IReadOnlyList<T> right) {
        string extraFail = (left.Count == right.Count) ? "" : $"Lengths are mismatched: {left.Count}, {right.Count}. ";
        for (int ii = 0; ii < left.Count && ii < right.Count; ++ii) {
            if (!Equals(left[ii], right[ii])) {
                if (left[ii] is not string && left[ii] is IEnumerable lie && right[ii] is IEnumerable rie) {
                    IEnumEq(lie.Cast<object?>(), rie.Cast<object?>());
                } else
                    Assert.Fail($"{extraFail}At index {ii}, left is {left[ii]} and right is {right[ii]}.");
            }
        }
        if (extraFail.Length > 0) {
            Assert.Fail(extraFail);
        }
    }

    public static void IEnumEq<T>(IEnumerable<T> left, IEnumerable<T> right) => ListEq(left.ToList(), right.ToList());
    
    public static void ThrowsAny(Action code) {
        try {
            code();
            Assert.Fail("Expected code to fail");
        } catch (Exception) {
            // ignored
        }
    }

    public static void ThrowsMessage(string? pattern, Action code) {
        try {
            code();
            if (pattern is not null)
                Assert.Fail($"Expected code to fail with error `{pattern}`, but it succeeded");
        } catch (Exception e) {
            if (pattern is null)
                Assert.Fail($"Expected code to succeed, but it failed with {e}");
            else
                RegexMatches(pattern, e.Message);
        }
    }

    public static bool CheckRegexMatches(string pattern, string message) =>
        new Regex(pattern, RegexOptions.Singleline).Match(message).Success;
    
    public static void RegexMatches(string pattern, string message) {
        if (!CheckRegexMatches(pattern, message)) {
            Assert.Fail($"Could not find pattern\n{pattern}\nin\n{message}\n");
        } else {
            Console.WriteLine($"Found pattern\n{pattern}\nin\n{message}\n");
        }
    }
    private const float err = 0.0001f;

    public static void VecEq(Vector2 left, Vector2 right) => VecEq(left, right, "");
    public static void VecEq(Vector2 left, Vector2 right, string msg, float error=err) {
        if (msg == "") msg = $"Comparing vector2s {left}, {right}";
        Assert.AreEqual(left.X, right.X, error, msg);
        Assert.AreEqual(left.Y, right.Y, error, msg);
    }
    public static void VecEq(Vector3 left, Vector3 right) => VecEq(left, right, "");
    public static void VecEq(Vector3 left, Vector3 right, string msg, float error=err) {
        msg += $": Comparing vector3s {left}, {right}";
        Assert.AreEqual(left.X, right.X, error, msg);
        Assert.AreEqual(left.Y, right.Y, error, msg);
        Assert.AreEqual(left.Z, right.Z, error, msg);
    }
    public static void VecEq(Vector4 left, Vector4 right) => VecEq(left, right, "");
    public static void VecEq(Vector4 left, Vector4 right, string msg, float error=err) {
        msg += $": Comparing vector4s {left}, {right}";
        Assert.AreEqual(left.X, right.X, error, msg);
        Assert.AreEqual(left.Y, right.Y, error, msg);
        Assert.AreEqual(left.Z, right.Z, error, msg);
        Assert.AreEqual(left.W, right.W, error, msg);
    }
}
}