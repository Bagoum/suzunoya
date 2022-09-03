using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BagoumLib.DataStructures {

/// <summary>
/// A class with a dictionary interface that is implemented without hashing by iterating over an array.
/// <br/>This is sometimes faster than a dictionary when the number of keys is very small (&lt;10).
/// </summary>
public class ArrayDictionary<V> : IEnumerable<(int Key, V Value)> {
    private int[] keys;
    private V[] values;
    private int count;
    public ArrayDictionary(int size = 8) {
        keys = new int[size];
        values = new V[size];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public V Get(in int key) {
        for (int ii = 0; ii < count; ++ii)
            if (key == keys[ii])
                return values[ii];
        throw new KeyNotFoundException();
    }
    
    public V this[in int key] {
        get {
            for (int ii = 0; ii < count; ++ii)
                if (key == keys[ii])
                    return values[ii];
            throw new KeyNotFoundException();
        }
        set {
            for (int ii = 0; ii < count; ++ii)
                if (key == keys[ii]) {
                    values[ii] = value;
                    return;
                }
            if (count >= keys.Length) {
                var nkeys = new int[keys.Length * 2];
                keys.CopyTo(nkeys, 0);
                keys = nkeys;
                var nvals = new V[nkeys.Length];
                values.CopyTo(nvals, 0);
                values = nvals;
            }
            keys[count] = key;
            values[count++] = value;
        }
    }
    public IEnumerator<(int Key, V Value)> GetEnumerator() {
        for (int ii = 0; ii < count; ++ii)
            yield return (keys[ii], values[ii]);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
}