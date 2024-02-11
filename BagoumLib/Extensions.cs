using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using JetBrains.Annotations;

namespace BagoumLib {
/// <summary>
/// Static class providing extensions
/// </summary>
[PublicAPI]
public static class Extensions {
    /// <summary>
    /// Return the number of occurrences of the character c in string s.
    /// </summary>
    public static int CountOf(this string s, char c) {
        int ct = 0;
        for (int ii = 0; ii < s.Length; ++ii) {
            if (s[ii] == c) ++ct;
        }
        return ct;
    }

    /// <summary>
    /// Try to get the character at index ii.
    /// </summary>
    public static char? TryIndex(this string s, int ii) {
        if (ii >= 0 && ii < s.Length) return s[ii];
        return null;
    }
    
    /// <summary>
    /// Try to get the character at index ii.
    /// </summary>
    public static bool TryIndex(this string s, int ii, out char c) {
        if (ii >= 0 && ii < s.Length) {
            c = s[ii];
            return true;
        }
        c = default;
        return false;
    }

    /// <summary>
    /// Return the string with the first character capitalized.
    /// </summary>
    public static string FirstToUpper(this string s) => s switch {
        "" => "",
        _ => s[0].ToString().ToUpper() + s[1..]
    };

    /// <summary>
    /// Convert a string into a string literal, eg. by converting newlines to explicit '\n',
    ///  converting hex characters to \u sequences, etc.
    /// </summary>
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

    /// <summary>
    /// Subscribe to an event, but dispose the token after it is triggered once.
    /// </summary>
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

    /// <summary>
    /// Dispose all <see cref="IDisposable"/> in a list.
    /// </summary>
    public static void DisposeAll(this List<IDisposable> tokens) {
        for (int ii = 0; ii < tokens.Count; ++ii)
            tokens[ii].Dispose();
        tokens.Clear();
    }

    /// <summary>
    /// Functor map over <see cref="IObservable{T}"/>
    /// </summary>
    public static IBObservable<U> Map<T, U>(this IBObservable<T> ev, Func<T, U> map) {
        var e =  new Event<T, U>(map);
        _ = ev.Subscribe(e);
        return e;
    }
    
    /// <summary>
    /// Functor map over <see cref="ICObservable{T}"/>
    /// </summary>
    public static ICObservable<U> Map<T, U>(this ICObservable<T> ev, Func<T, U> map) {
        var e = new Evented<U>(map(ev.Value));
        _ = ev.Subscribe(x => e.OnNext(map(x)), e.OnError, e.OnCompleted);
        return e;
    }
    
    /// <summary>
    /// Get the cross product of elements from the first stream and second stream.
    /// <br/>This will include pairs where the first element occurs after the second element,
    ///  unlike the behavior of ae.SelectMany(_ => be, (a, b) => (a, b)).
    /// </summary>
    public static IBObservable<(A a, B b)> CrossProduct<A, B>(this IObservable<A> ae, IObservable<B> be) {
        var ev = (ae is ICObservable<A> fce && be is ICObservable<B> bce) ?
            (IBSubject<(A, B)>)new Evented<(A, B)>((fce.Value, bce.Value)) :
            new Event<(A, B)>();
        var avs = new List<A>();
        var bvs = new List<B>();
        ae.Subscribe(a => {
            avs.Add(a);
            foreach (var b in bvs)
                ev.OnNext((a, b));
        });
        be.Subscribe(b => {
            bvs.Add(b);
            foreach (var a in avs)
                ev.OnNext((a, b));
        });
        return ev;
    }

    /// <summary>
    /// Applicative apply over IObservable. This will compute the cross product of values,
    ///  unlike the naive definition `from f in fe from x in xe select f(x)`, which
    ///  does not pull tuples where f occurs after x.
    /// </summary>
    public static IObservable<B> Apply<A, B>(this IObservable<Func<A, B>> fe, IObservable<A> xe) =>
        fe.CrossProduct(xe).Map(fx => fx.a(fx.b));

    /// <summary>
    /// Monadic bind over <see cref="IObservable{T}"/>. Note this leaves hanging disposables.
    /// </summary>
    public static IObservable<U> Bind<T, U>(this IObservable<T> obj, Func<T, IObservable<U>> element) {
        var ev = new Event<U>();
        _ = obj.Subscribe(o => element(o).Subscribe(ev));
        return ev;
    }
    
    /*
    /// <summary>
    /// Monadic bind over <see cref="IObservable{T}"/>. Note this leaves hanging disposables.
    /// </summary>
    public static Parser<R> SelectMany<A, B, R>(this Parser<A> p, Func<A, Parser<B>> f, Func<A, B, R> project)
        => p.Bind(f, project);
*/
    /// <summary>
    /// Performs a monadic bind over <see cref="IObservable{T}"/> and then subscribes to the constructed event.
    /// Collects all generated <see cref="IDisposable"/> tokens together, so that when the returned token is disposed,
    ///  the constructed event is also disposed.
    /// </summary>
    public static IDisposable BindSubscribe<T, U>(this IObservable<T> obj, Func<T, IObservable<U>?> element,
        Action<U> cb) {
        var tokens = new List<IDisposable>();
        tokens.Add(obj.Subscribe(o => {
            if (element(o) is { } ev)
                tokens.Add(ev.Subscribe(cb));
        }));
        return new ListDisposable(tokens);
    }
}

/// <summary>
/// Static class providing array extensions
/// </summary>
[PublicAPI]
public static class ArrayExtensions {
    /// <summary>
    /// Insert an element into an array, shifting all existing elements starting at the given index right by one.
    /// </summary>
    public static void Insert<T>(this T[] arr, ref int count, T obj, int at) {
        if (count == arr.Length) throw new IndexOutOfRangeException();
        Array.Copy(arr, at, arr, at + 1, count++ - at);
        arr[at] = obj;
    }

    /// <summary>
    /// Join two arrays.
    /// </summary>
    public static T[] Extend<T>(this T[] first, T[] second) {
        var ret = new T[first.Length + second.Length];
        Array.Copy(first, 0, ret, 0, first.Length);
        Array.Copy(second, 0, ret, first.Length, second.Length);
        return ret;
    }

    /// <summary>
    /// Index into an array, but loop around to the start if the index is out of bounds.
    /// </summary>
    public static T ModIndex<T>(this IList<T> arr, int index) => arr[BMath.Mod(arr.Count, index)];

    /// <summary>
    /// Try to get the object at the given index.
    /// </summary>
    public static T? Try<T>(this IList<T> arr, int index) where T : class {
        if (index >= 0 && index < arr.Count) return arr[index];
        return null;
    }

    /// <summary>
    /// Try to get the object at the given index.
    /// </summary>
    public static T? TryN<T>(this IList<T> arr, int index) where T : struct {
        if (index >= 0 && index < arr.Count) return arr[index];
        return null;
    }

    /// <summary>
    /// Try to get the object at the given index.
    /// </summary>
    public static bool Try<T>(this T[] arr, int index, out T res) {
        if (index >= 0 && index < arr.Length) {
            res = arr[index];
            return true;
        }
        res = default!;
        return false;
    }

    /// <summary>
    /// Get the object at the given index, or return the default value.
    /// </summary>
    public static T Try<T>(this T[] arr, int index, T deflt) {
        if (index >= 0 && index < arr.Length) return arr[index];
        return deflt;
    }

    /// <summary>
    /// Get the index of the given object, or return -1 if not found.
    /// </summary>
    public static int IndexOf<T>(this T[] arr, T obj) {
        for (int ii = 0; ii < arr.Length; ++ii) {
            if (EqualityComparer<T>.Default.Equals(arr[ii], obj)) return ii;
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

/// <summary>
/// An object which has a sequence of values, some of which may themselves be
///  objects with sequences of values.
/// <br/>See <see cref="IEnumExtensions.Unroll{T}"/>
/// </summary>
[PublicAPI]
public interface IUnrollable<T> {
    /// <summary>
    /// Values
    /// </summary>
    IEnumerable<T> Values { get; }
}

/// <summary>
/// Static class providing extensions for <see cref="IEnumerable{T}"/>
/// </summary>
[PublicAPI]
public static class IEnumExtensions {
    /// <summary>
    /// Try to get the element at the given index in the enumerable.
    /// <br/>If the enumerable is too short, return Maybe.None.
    /// </summary>
    public static Maybe<T> TryGetAt<T>(this IEnumerable<T> arr, int index) {
        if (arr is IList<T> l) {
            return index < l.Count ? l[index] : Maybe<T>.None;
        }
        foreach (var x in arr)
            if (index-- == 0)
                return x;
        return Maybe<T>.None;
    }
    
    /// <summary>
    /// Get successive elements of an enumerable.
    /// </summary>
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

    /// <summary>
    /// Return `arr.Append(next)` if next is nnonnull. Otherwise, return `arr`.
    /// </summary>
    public static IEnumerable<T> AppendIfNonnull<T>(this IEnumerable<T> arr, T? next) where T : class {
        return (next == null) ? arr : arr.Append(next);
    }
    /// <summary>
    /// Return `arr.Append(next)` if next is nnonnull. Otherwise, return `arr`.
    /// </summary>
    public static IEnumerable<T> AppendIfNonnull<T>(this IEnumerable<T> arr, T? next) where T : struct {
        return (next.Try(out var x)) ? arr.Append(x) : arr;
    }
    
    /// <summary>
    /// Get the index and element of each element in the enumerable.
    /// <br/>Similar to python enumerate
    /// </summary>
    public static IEnumerable<(int idx, T val)> Enumerate<T>(this IEnumerable<T> arr) => arr.Select((x, i) => (i, x));

    /// <summary>
    /// Generate an <see cref="IDisposable"/> from each entry in a list, then join them into a single disposable.
    /// </summary>
    public static IDisposable SelectDisposable<T>(this IEnumerable<T> arr, Func<T, IDisposable> disposer) =>
        ListDisposable.From(arr, disposer);

    /// <summary>
    /// If an element is an <see cref="IUnrollable{T}"/>, then unroll it and yield all its unrolled elements.
    /// Otherwise, yield the element.
    /// </summary>
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

    /// <summary>
    /// Returns the range [0, max)
    /// </summary>
    public static IEnumerable<int> Range(this int max) {
        for (int ii = 0; ii < max; ++ii) yield return ii;
    }

    /// <summary>
    /// Returns the range [min, max)
    /// </summary>
    public static IEnumerable<int> Range(this (int min, int max) bound) {
        for (int ii = bound.min; ii < bound.max; ++ii) yield return ii;
    }

    /// <summary>
    /// Returns the range [min, max) with the provided step
    /// </summary>
    public static IEnumerable<double> Step(this (double min, double max) bound, double step) {
        for (double x = bound.min; x < bound.max; x += step) yield return x;
    }
    
    /// <summary>
    /// Maps over each element, and only returns the non-null ones.
    /// </summary>
    public static IEnumerable<U> SelectNotNull<T, U>(this IEnumerable<T> arr, Func<T, U?> f) where U : class {
        foreach (var x in arr) {
            var y = f(x);
            if (y != null) yield return y;
        }
    }

    /// <summary>
    /// Maps over each element, and only returns the non-null ones.
    /// </summary>
    public static IEnumerable<U> SelectNotNull<T, U>(this IEnumerable<T> arr, Func<T, U?> f) where U : struct {
        foreach (var x in arr) {
            var y = f(x);
            if (y.HasValue) yield return y.Value;
        }
    }

    /// <summary>
    /// Get the index of the first object satisfying the predicate, or -1 if none is found.
    /// </summary>
    public static int IndexOf<T>(this IEnumerable<T> arr, Func<T, bool> pred) {
        int i = 0;
        foreach (var x in arr) {
            if (pred(x)) return i;
            ++i;
        }
        return -1;
    }

    /// <inheritdoc cref="ArrayExtensions.IndexOf{T}"/>
    public static int IndexOf<T>(this IEnumerable<T> arr, T obj) {
        int i = 0;
        foreach (var x in arr) {
            if (EqualityComparer<T>.Default.Equals(obj, x)) return i;
            ++i;
        }
        return -1;
    }
    
    /// <summary>
    /// Return the first element of an enumerable, or null if it is empty.
    /// </summary>
    public static T? FirstOrNull<T>(this IEnumerable<T> arr) where T : struct {
        foreach (var x in arr) return x;
        return null;
    }

    /// <summary>
    /// Filter out null elements.
    /// </summary>
    public static IEnumerable<T> FilterNone<T>(this IEnumerable<T?> arr) where T : struct {
        foreach (var x in arr) {
            if (x.Try(out var y)) yield return y;
        }
    }
    
    /// <summary>
    /// Filter out null elements.
    /// </summary>
    public static IEnumerable<T> FilterNone<T>(this IEnumerable<T?> arr) where T : class {
        foreach (var x in arr) {
            if (x != null) yield return x;
        }
    }

    /// <summary>
    /// Filter out null elements.
    /// </summary>
    public static IEnumerable<T> FilterMaybe<T>(this IEnumerable<Maybe<T>> arr) {
        foreach (var x in arr)
            if (x.Valid) yield return x.Value;
    }

    /// <summary>
    /// Enumerate over all key-value pairs in the dictionary.
    /// </summary>
    public static IEnumerable<(K key, V value)> Items<K, V>(this Dictionary<K, V> dict) where K : notnull
        => dict.Keys.Select(k => (k, dict[k]));

    /// <summary>
    /// Simplify groupings into (key, values) pairs.
    /// </summary>
    public static IEnumerable<(K key, V[] values)> GroupToArray<K, V>(this IEnumerable<IGrouping<K, V>> grp) =>
        grp.Select(g => (g.Key, g.ToArray()));

    /// <summary>
    /// Get the grouping with the largest value set.
    /// </summary>
    public static (K key, V[] values) MaxByGroupSize<K, V>(this IEnumerable<IGrouping<K, V>> grp) =>
        grp.GroupToArray().MaxBy(g => g.values.Length);

    /// <summary>
    /// Get the maximum element according to the given selector.
    /// </summary>
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
    
    /// <summary>
    /// Get the maximum element according to the given selector, then return
    ///  both the element and its selected value.
    /// </summary>
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

    /// <summary>
    /// Flatten a once-nested enumerable.
    /// </summary>
    public static IEnumerable<T> Join<T>(this IEnumerable<IEnumerable<T>> arrs) {
        foreach (var arr in arrs) {
            foreach (var x in arr) {
                yield return x;
            }
        }
    }

    /// <summary>
    /// Convert key-value pairs into a dictionary.
    /// </summary>
    public static Dictionary<K, V> ToDict<K, V>(this IEnumerable<(K, V)> arr) where K : notnull {
        var dict = new Dictionary<K, V>();
        foreach (var (k, v) in arr) dict[k] = v;
        return dict;
    }
    
    /// <summary>
    /// Separate the given enumerables by a separator.
    /// <br/>eg. SeparateBy({1,2,3}, {4,5,6}, {7,8,9}, 0) = {1,2,3,0,4,5,6,0,7,8,9}
    /// </summary>
    public static IEnumerable<T> SeparateBy<T>(this IEnumerable<IEnumerable<T>> arrs, T sep) {
        bool first = true;
        foreach (var arr in arrs) {
            if (!first) yield return sep;
            first = false;
            foreach (var x in arr) yield return x;
        }
    }

    /// <summary>
    /// Get the maximum number of consecutive occurences of the given object. 
    /// </summary>
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

/// <summary>
/// Static class providing extensions for <see cref="List{T}"/>
/// </summary>
[PublicAPI]
public static class ListExtensions {
    /// <summary>
    /// Get a hash code based only on the elements of the list.
    /// </summary>
    public static int ElementWiseHashCode<T>(this IReadOnlyList<T> arr) {
        int result = 17;
        for (int ii = 0; ii < arr.Count; ++ii) {
            unchecked {
                result = result * 23 + (arr[ii]?.GetHashCode() ?? 0);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns true if the lists contains the same elements.
    /// </summary>
    public static bool AreSame<T>(this IReadOnlyList<T>? arr, IReadOnlyList<T>? other) {
        if (arr == null && other == null) return true;
        if (arr == null || other == null) return false;
        if (arr.Count != other.Count)
            return false;
        for (int ii = 0; ii < arr.Count; ++ii)
            if (!EqualityComparer<T>.Default.Equals(arr[ii], other[ii]))
                return false;
        return true;
    }
    
    /// <summary>
    /// Add elements from <paramref name="from"/> into <paramref name="into"/> if <paramref name="into"/> exists,
    ///  otherwise assign <paramref name="from"/> to <paramref name="into"/>.
    /// </summary>
    /// <param name="from"></param>
    /// <param name="into"></param>
    /// <typeparam name="T"></typeparam>
    public static void AssignOrExtend<T>(this List<T> from, ref List<T>? into) {
        if (into == null) into = from;
        else into.AddRange(from);
    }

    /// <summary>
    /// Add 1 to idx, but reset it to zero if it is larger than the list length.
    /// </summary>
    public static void IncrLoop<T>(this List<T> arr, ref int idx) => arr.Count.IncrLoop(ref idx);

    /// <summary>
    /// Subtract 1 from idx, but reset it to ^1 if it is less than zero.
    /// </summary>
    public static void DecrLoop<T>(this List<T> arr, ref int idx) => arr.Count.DecrLoop(ref idx);

    /// <summary>
    /// Add 1 to idx, but reset it to zero if it is larger than mod.
    /// </summary>
    public static void IncrLoop(this int mod, ref int idx) {
        if (++idx >= mod) idx = 0;
    }

    /// <summary>
    /// Subtract 1 from idx, but reset it to mod-1 if it is less than zero.
    /// </summary>
    public static void DecrLoop(this int mod, ref int idx) {
        if (--idx < 0) idx = mod - 1;
    }
}

/// <summary>
/// Static class providing extensions for dictionaries
/// </summary>
[PublicAPI]
public static class DictExtensions {
    /// <summary>
    /// Get the element at the given key, or throw an exception explicitly mentioning the key.
    /// </summary>
    public static V GetOrThrow<K, V>(this Dictionary<K, V> dict, K key) where K : notnull {
        if (dict.TryGetValue(key, out var res)) return res;
        throw new Exception($"Key \"{key}\" does not exist.");
    }

    /// <summary>
    /// Get the element at the given key, or throw an exception explicitly mentioning the key and the dictionary name.
    /// </summary>
    public static V GetOrThrow<K, V>(this IReadOnlyDictionary<K, V> dict, K key, string indict) {
        if (dict.TryGetValue(key, out var res)) return res;
        throw new Exception($"Key \"{key}\" does not exist in the dictionary {indict}.");
    }

    /// <summary>
    /// Try to get the element at the given key.
    /// </summary>
    public static V? MaybeGet<K, V>(this IReadOnlyDictionary<K, V> dict, K key) where V : struct {
        if (dict.TryGetValue(key, out var res)) return res;
        return null;
    }

    /// <summary>
    /// For a dictionary whose values are lists, add an element to the list associated with the given key,
    ///  or create a new list if none are yet associated.
    /// </summary>
    public static void AddToList<K, V>(this Dictionary<K, List<V>> dict, K key, V value) where K : notnull {
        if (!dict.TryGetValue(key, out var l)) {
            dict[key] = l = new List<V>();
        }
        l.Add(value);
    }

    /// <summary>
    /// For a nested dictionary, returns true iff dict[key1][key2] exists.
    /// </summary>
    public static bool Has2<K, K2, V>(this Dictionary<K, Dictionary<K2, V>> dict, K key, K2 key2) where K : notnull where K2 : notnull =>
        dict.TryGetValue(key, out var dct2) && dct2.ContainsKey(key2);

    /// <summary>
    /// For a nested dictionary, set dict[key1][key2] = val. Creates dict[key1] if it does not exist.
    /// </summary>
    public static void Add2<K, K2, V>(this Dictionary<K, Dictionary<K2, V>> dict, K key, K2 key2, V val) where K2 : notnull where K : notnull {
        if (!dict.TryGetValue(key, out var dct2))
            dct2 = dict[key] = new Dictionary<K2, V>();
        dct2[key2] = val;
    }
    
    /// <summary>
    /// For a nested dictionary, try to get the value at dict[key1][key2].
    /// </summary>
    public static bool TryGet2<K, K2, V>(this Dictionary<K, Dictionary<K2, V>> dict, K key, K2 key2, out V val) where K2 : notnull where K : notnull {
        val = default!;
        return dict.TryGetValue(key, out var dct2) && dct2.TryGetValue(key2, out val!);
    }

    /// <summary>
    /// Get the value at the given key, or create a new value.
    /// </summary>
    public static V SetDefault<K, V>(this Dictionary<K, V> dict, K key) where V : new() where K : notnull {
        if (!dict.TryGetValue(key, out var data)) {
            data = dict[key] = new V();
        }
        return data;
    }

    /// <summary>
    /// Get the value at the given key, or create a new value.
    /// </summary>
    public static V SetDefault<K, V>(this Dictionary<K, V> dict, K key, V deflt) where K : notnull {
        if (!dict.TryGetValue(key, out var data)) {
            data = dict[key] = deflt;
        }
        return data;
    }
}

/// <summary>
/// Static class providing extensions for nullable struct types
/// </summary>
[PublicAPI]
public static class NullableExtensions {
    /// <summary>
    /// Returns x &amp;&amp; y (or null if x is null).
    /// </summary>
    public static bool? And(this bool? x, bool y) => x.HasValue ? (bool?) (x.Value && y) : null;
    /// <summary>
    /// Returns x || y (or null if x is null).
    /// </summary>
    public static bool? Or(this bool? x, bool y) => x.HasValue ? (bool?) (x.Value || y) : null;

    /// <summary>
    /// Functor map over nullable structs
    /// </summary>
    public static U? FMap<T, U>(this T? x, Func<T, U> f) where T : struct where U : struct
        => x.HasValue ? (U?) f(x.Value) : null;

    /// <summary>
    /// Monadic bind over nullable structs
    /// </summary>
    public static U? Bind<T, U>(this T? x, Func<T, U?> f) where T : struct where U : struct
        => x.HasValue ? f(x.Value) : null;

    /// <summary>
    /// Try to get the value of a nullable struct.
    /// </summary>
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

    /// <summary>
    /// Isomorphic transformation between nullable struct and <see cref="Maybe{T}"/>
    /// </summary>
    public static Maybe<T> AsMaybe<T>(this T? x) where T : struct =>
        x.HasValue ? 
            new Maybe<T>(x.Value) : 
            Maybe<T>.None;

    /// <summary>
    /// Try to get the value of a nullable reference type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Try<T>(this T? x, out T y) where T : class {
        y = x!;
        return x != null;
    }
}

/// <summary>
/// Static class providing extensions for miscellaneous data structures
/// </summary>
[PublicAPI]
public static class DataStructureExtensions {
    /// <summary>
    /// Try to get the top element in a stack.
    /// </summary>
    public static T? TryPeek<T>(this Stack<T> stack) where T : class =>
        stack.Count > 0 ? stack.Peek() : null;
    /// <summary>
    /// Try to get the top element in a <see cref="StackList{T}"/>.
    /// </summary>
    public static T? TryPeek<T>(this StackList<T> stack) where T : class =>
        stack.Count > 0 ? stack.Peek() : null;
}

/// <summary>
/// Static class providing extensions for events
/// </summary>
[PublicAPI]
public static class EventExtensions {

    /// <summary>
    /// Get an <see cref="IObservable{Unit}"/> that is triggered when the source event is triggered,
    ///  but does not carry its value.
    /// </summary>
    public static IBObservable<Unit> Erase<T>(this IBObservable<T> ev) => ev.Map(_ => Unit.Default);
}

}