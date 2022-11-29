using System;
using System.Collections.Generic;

namespace BagoumLib.Sorting {

/// <summary>
/// An implementation of merge sort that sorts elements back and forth from the buffer
/// in order to minimize buffer copies.
/// </summary>
public class AlternatingMergeSorter<T> : ISorter<T> {
    private void Sort(T[] array, int start, int end, LeqCompare<T> comp, T[] buffer, bool sortIntoBuffer) {
        var len = end - start;
        if (len == 0) return;
        if (len == 1) {
            if (sortIntoBuffer)
                buffer[0] = array[start];
            return;
        }

        int mid = start + len/2;
        var alen = mid - start;
        var blen = end - mid;
        
        //First sort B into the latter half of the array
        Sort(array, mid, end, comp, buffer, false);
        //Sort A into the non-target array, which may be the buffer
        Sort(array, start, mid, comp, buffer, !sortIntoBuffer);

        var target = sortIntoBuffer ? buffer : array;
        var asrc = sortIntoBuffer ? array : buffer;
        var astart = sortIntoBuffer ? start : 0;
        var ti = sortIntoBuffer ? 0 : start;
        int act = 0, bct = 0;
        while (act < alen && bct < blen) {
            if (comp(in asrc[astart + act], in array[mid + bct])) {
                target[ti++] = asrc[astart + act++];
            } else {
                target[ti++] = array[mid + bct++];
            }
        }
        //Copy any remaining elements
        Array.Copy(asrc, astart + act, target, ti, alen - act);
        if (sortIntoBuffer)
            Array.Copy(array, mid + bct, buffer, ti, blen - bct);
    }
    
    /// <summary>
    /// Sort an array using mergesort.
    /// </summary>
    /// <param name="array">Array to sort</param>
    /// <param name="start">Starting index for sort (inclusive)</param>
    /// <param name="end">Ending index for sort (exclusive)</param>
    /// <param name="comp">Sort comparer</param>
    /// <param name="buffer">Working buffer. Must have at least (end-start)/2 capacity</param>
    public void Sort(T[] array, int start, int end, LeqCompare<T> comp, T[] buffer) {
        Sort(array, start, end, comp, buffer, false);
    }

    /// <summary>
    /// See <see cref="Sort(T[],int,int,BagoumLib.Sorting.LeqCompare{T},T[])"/>
    /// </summary>
    public void Sort(T[] array, int start, int end, LeqCompare<T> comp) =>
        Sort(array, start, end, comp, new T[(end - start + 1) / 2]);

    /// <summary>
    /// See <see cref="Sort(T[],int,int,BagoumLib.Sorting.LeqCompare{T},T[])"/>
    /// </summary>
    public void Sort(T[] array, LeqCompare<T> comp) => Sort(array, 0, array.Length, comp);
}

}