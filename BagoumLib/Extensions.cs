using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib {
[PublicAPI]
public static class Extensions {
    public static int CountOf(this string s, char c) {
        int ct = 0;
        for (int ii = 0; ii < s.Length; ++ii) {
            if (s[ii] == c) ++ct;
        }
        return ct;
    }

    public static char? TryIndex(this string s, int ii) {
        if (ii >= 0 && ii < s.Length) return s[ii];
        return null;
    }
    public static bool TryIndex(this string s, int ii, out char c) {
        if (ii >= 0 && ii < s.Length) {
            c = s[ii];
            return true;
        }
        c = default;
        return false;
    }

    public static string ToLiteral(this string s) {
        var sb = new StringBuilder(s.Length + 10);
        sb.Append('"');
        foreach (var c in s) {
            if (     c == '"')
                sb.Append("\\\"");
            else if (c == '\\')
                sb.Append("\\");
            else if (c == '\n')
                sb.Append("\\n");
            else if (c == '\r')
                sb.Append("\\n");
            else if (c == '\t')
                sb.Append("\\t");
            else if (c >= 0x20 && c <= 0x7e) {
                sb.Append(c);
            } else {
                sb.Append("\\u");
                sb.Append(((int)c).ToString("x4"));
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// If x is null or empty, return y. Else return x.
    /// </summary>
    public static string Or(this string? x, string y) => string.IsNullOrEmpty(x) ? y : x;

    public static IDisposable SubscribeOnce<T>(this IObservable<T> ev, Action<T> listener) {
        bool disposed = false;
        IDisposable? token = null;
        token = ev.Subscribe(x => {
            disposed = true;
            // ReSharper disable once AccessToModifiedClosure
            token?.Dispose();
            listener(x);
        });
        if (disposed)
            token.Dispose();
        return token;
    }

    public static void DisposeAll(this List<IDisposable> tokens) {
        for (int ii = 0; ii < tokens.Count; ++ii)
            tokens[ii].Dispose();
        tokens.Clear();
    }
}

[PublicAPI]
public static class ArrayExtensions {

    public static void Insert<T>(this T[] arr, ref int count, T obj, int at) {
        if (count == arr.Length) throw new IndexOutOfRangeException();
        Array.Copy(arr, at, arr, at + 1, count++ - at);
        arr[at] = obj;
    }

    public static T[] Extend<T>(this T[] first, T[] second) {
        var ret = new T[first.Length + second.Length];
        Array.Copy(first, 0, ret, 0, first.Length);
        Array.Copy(second, 0, ret, first.Length, second.Length);
        return ret;
    }

    public static T ModIndex<T>(this IList<T> arr, int index) => arr[BMath.Mod(arr.Count, index)];

    public static T? Try<T>(this IList<T> arr, int index) where T : class {
        if (index >= 0 && index < arr.Count) return arr[index];
        return null;
    }

    public static T? TryN<T>(this IList<T> arr, int index) where T : struct {
        if (index >= 0 && index < arr.Count) return arr[index];
        return null;
    }

    public static bool Try<T>(this T[] arr, int index, out T res) where T : class {
        if (index >= 0 && index < arr.Length) {
            res = arr[index];
            return true;
        }
        res = null!;
        return false;
    }

    public static T Try<T>(this T[] arr, int index, T deflt) {
        if (index >= 0 && index < arr.Length) return arr[index];
        return deflt;
    }

    /// <summary>
    /// Returns -1 if not found
    /// </summary>
    public static int IndexOf<T>(this T[] arr, T obj) {
        for (int ii = 0; ii < arr.Length; ++ii) {
            if (Equals(arr[ii], obj)) return ii;
        }
        return -1;
    }

    /// <summary>
    /// Returns the first T such that the associated priority is LEQ the given priority.
    /// Make sure the array is sorted from lowest to highest priority.
    /// </summary>
    public static T GetBounded<T>(this (int priority, T)[] arr, int priority, T deflt) {
        var result = deflt;
        for (int ii = 0; ii < arr.Length; ++ii) {
            if (priority >= arr[ii].priority) result = arr[ii].Item2;
            else break;
        }
        return result;
    }
}

[PublicAPI]
public interface IUnrollable<T> {
    IEnumerable<T> Values { get; }
}

[PublicAPI]
public static class IEnumExtensions {
    public static IEnumerable<(T, T)> PairSuccessive<T>(this IEnumerable<T> arr, T last) {
        T prev = default!;
        int ct = 0;
        foreach (var x in arr) {
            if (ct++ > 0)
                yield return (prev, x);
            prev = x;
        }
        if (ct > 0)
            yield return (prev, last);
    }
    public static IEnumerable<(int idx, T val)> Enumerate<T>(this IEnumerable<T> arr) => arr.Select((x, i) => (i, x));

    public static IDisposable SelectDisposable<T>(this IEnumerable<T> arr, Func<T, IDisposable> disposer) =>
        ListDisposable.From(arr, disposer);

    public static void ForEach<T>(this IEnumerable<T> arr, Action<T> act) {
        foreach (var ele in arr) {
            act(ele);
        }
    }

    public static void ForEachI<T>(this IEnumerable<T> arr, Action<int, T> act) {
        foreach (var (i, ele) in arr.Enumerate()) {
            act(i, ele);
        }
    }

    public static IEnumerable<T> Unroll<T>(this IEnumerable<T> arr) {
        foreach (var p in arr) {
            if (p is IUnrollable<T> ur) {
                foreach (var res in ur.Values.Unroll()) 
                    yield return res;
            } else {
                yield return p;
            }
        }
    }

    public static IEnumerable<int> Range(this int max) {
        for (int ii = 0; ii < max; ++ii) yield return ii;
    }

    public static IEnumerable<int> Range(this (int min, int max) bound) {
        for (int ii = bound.min; ii < bound.max; ++ii) yield return ii;
    }

    public static IEnumerable<double> Step(this (double min, double max) bound, double step) {
        for (double x = bound.min; x < bound.max; x += step) yield return x;
    }
    public static IEnumerable<U> SelectNotNull<T, U>(this IEnumerable<T> arr, Func<T, U?> f) where U : class {
        foreach (var x in arr) {
            var y = f(x);
            if (y != null) yield return y;
        }
    }
    public static IEnumerable<U> SelectNotNull<T, U>(this IEnumerable<T> arr, Func<T, U?> f) where U : struct {
        foreach (var x in arr) {
            var y = f(x);
            if (y.HasValue) yield return y.Value;
        }
    }

    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> arr) where T : class => arr.Where(x => x != null)!;

    public static int IndexOf<T>(this IEnumerable<T> arr, Func<T, bool> pred) {
        int i = 0;
        foreach (var x in arr) {
            if (pred(x)) return i;
            ++i;
        }
        return -1;
    }

    public static int IndexOf<T>(this IEnumerable<T> arr, T obj) {
        int i = 0;
        foreach (var x in arr) {
            if (Equals(obj, x)) return i;
            ++i;
        }
        return -1;
    }
    public static T? FirstOrNull<T>(this IEnumerable<T> arr) where T : struct {
        foreach (var x in arr) return x;
        return null;
    }

    public static IEnumerable<T> FilterNone<T>(this IEnumerable<T?> arr) where T : struct {
        foreach (var x in arr) {
            if (x.Try(out var y)) yield return y;
        }
    }
    public static IEnumerable<T> FilterNone<T>(this IEnumerable<T?> arr) where T : class {
        foreach (var x in arr) {
            if (x != null) yield return x;
        }
    }

    public static IEnumerable<(K key, V value)> Items<K, V>(this Dictionary<K, V> dict) where K : notnull
        => dict.Keys.Select(k => (k, dict[k]));

    public static IEnumerable<(K key, V[] values)> GroupToArray<K, V>(this IEnumerable<IGrouping<K, V>> grp) =>
        grp.Select(g => (g.Key, g.ToArray()));

    public static (K key, V[] values) MaxByGroupSize<K, V>(this IEnumerable<IGrouping<K, V>> grp) =>
        grp.GroupToArray().MaxBy(g => g.values.Length);

    public static T MaxBy<T, U>(this IEnumerable<T> arr, Func<T, U> selector) where U : IComparable<U> {
        bool first = true;
        T obj = default!;
        U val = default!;
        foreach (var item in arr) {
            if (first) {
                first = false;
                obj = item;
                val = selector(item);
            } else {
                var nextVal = selector(item);
                if (nextVal.CompareTo(val) > 0) {
                    obj = item;
                    val = nextVal;
                }
            }
        }
        return obj;
    }
    public static (T obj, U val) MaxByWith<T, U>(this IEnumerable<T> arr, Func<T, U> selector) where U : IComparable<U> {
        bool first = true;
        T obj = default!;
        U val = default!;
        foreach (var item in arr) {
            if (first) {
                first = false;
                obj = item;
                val = selector(item);
            } else {
                var nextVal = selector(item);
                if (nextVal.CompareTo(val) > 0) {
                    obj = item;
                    val = nextVal;
                }
            }
        }
        return (obj, val);
    }
    
    /// <summary>
    /// Skip the first SKIP elements of the enumerable.
    /// If there are fewer than SKIP elements, then return an empty enumerable instead of throwing.
    /// </summary>
    public static IEnumerable<T> SoftSkip<T>(this IEnumerable<T> arr, int skip) {
        foreach (var x in arr) {
            if (skip-- <= 0) yield return x;
        }
    }

    public static IEnumerable<T> Join<T>(this IEnumerable<IEnumerable<T>> arrs) {
        foreach (var arr in arrs) {
            foreach (var x in arr) {
                yield return x;
            }
        }
    }

    public static Dictionary<K, V> ToDict<K, V>(this IEnumerable<(K, V)> arr) where K : notnull {
        var dict = new Dictionary<K, V>();
        foreach (var (k, v) in arr) dict[k] = v;
        return dict;
    }
    
    public static IEnumerable<T> SeparateBy<T>(this IEnumerable<IEnumerable<T>> arrs, T sep) {
        bool first = true;
        foreach (var arr in arrs) {
            if (!first) yield return sep;
            first = false;
            foreach (var x in arr) yield return x;
        }
    }

    public static int MaxConsecutive<T>(this IList<T> arr, T obj) where T:IEquatable<T> {
        int max = 0;
        int curr = 0;
        for (int ii = 0; ii < arr.Count; ++ii) {
            if (arr[ii].Equals(obj)) {
                if (++curr > max) max = curr;
            } else {
                curr = 0;
            }
        }
        return max;
    }
}

[PublicAPI]
public static class ListExtensions {
    public static bool ContainsSame<T>(this IList<T> a, IList<T> b) {
        if (a.Count != b.Count)
            return false;
        for (int ii = 0; ii < a.Count; ++ii)
            if (!Equals(a[ii], b[ii]))
                return false;
        return true;
    }
    public static void AssignOrExtend<T>(this List<T> from, ref List<T>? into) {
        if (into == null) into = from;
        else into.AddRange(from);
    }

    public static void IncrLoop<T>(this List<T> arr, ref int idx) => arr.Count.IncrLoop(ref idx);

    public static void DecrLoop<T>(this List<T> arr, ref int idx) => arr.Count.DecrLoop(ref idx);

    public static void IncrLoop(this int mod, ref int idx) {
        if (++idx >= mod) idx = 0;
    }

    public static void DecrLoop(this int mod, ref int idx) {
        if (--idx < 0) idx = mod - 1;
    }
}

[PublicAPI]
public static class NullableExtensions {
    public static bool? And(this bool? x, bool y) => x.HasValue ? (bool?) (x.Value && y) : null;
    public static bool? Or(this bool? x, bool y) => x.HasValue ? (bool?) (x.Value || y) : null;

    public static U? FMap<T, U>(this T? x, Func<T, U> f) where T : struct where U : struct
        => x.HasValue ? (U?) f(x.Value) : null;

    public static U? Bind<T, U>(this T? x, Func<T, U?> f) where T : struct where U : struct
        => x.HasValue ? f(x.Value) : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Try<T>(this T? x, out T y) where T : struct {
        if (x.HasValue) {
            y = x.Value;
            return true;
        } else {
            y = default;
            return false;
        }
    }

    public static Maybe<T> AsMaybe<T>(this T? x) where T : struct =>
        x.HasValue ? 
            new Maybe<T>(x.Value) : 
            Maybe<T>.None;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Try<T>(this T? x, out T y) where T : class {
        y = x!;
        return x != null;
    }
}

[PublicAPI]
public static class DataStructureExtensions {
    public static T? TryPeek<T>(this Stack<T> stack) where T : class =>
        stack.Count > 0 ? stack.Peek() : null;
    public static T? TryPeek<T>(this StackList<T> stack) where T : class =>
        stack.Count > 0 ? stack.Peek() : null;
}

}