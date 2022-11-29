using System;
using System.Collections.Generic;

namespace BagoumLib.Sorting {

/// <summary>
/// An implementation of mergesort that uses a buffer for extra space.
/// </summary>
public class MergeSorter<T> : ISorter<T> {
    /// <summary>
    /// Sort an array using mergesort.
    /// </summary>
    /// <param name="array">Array to sort</param>
    /// <param name="start">Starting index for sort (inclusive)</param>
    /// <param name="end">Ending index for sort (exclusive)</param>
    /// <param name="comp">Sort comparer</param>
    /// <param name="buffer">Working buffer. Must have at least (end-start)/2 capacity</param>
    public void Sort(T[] array, int start, int end, LeqCompare<T> comp, T[] buffer) {
        var len = end - start;
        if (len < 2) {
            for (int i = start + 1; i < end; i++) {
                ref var temp = ref array[i];
                int j;
                for (j = i; j > start && !comp(in array[j - 1], in temp); --j)
                    array[j] = array[j - 1];
                array[j] = temp;
            }
            return;
        }
		
        int mid = start + len/2;
        var alen = mid - start;
        var blen = end - mid;
        Sort(array, start, mid, comp, buffer);
        Sort(array, mid, end, comp, buffer);
		
        // standard merge operation here (only A is copied to the buffer)
        Array.Copy(array, start, buffer, 0, alen);
        int A_count = 0, B_count = 0;
        while (A_count < alen && B_count < blen) {
            array[start++] = comp(in buffer[A_count], in array[mid + B_count]) ?
                buffer[A_count++] :
                array[mid + B_count++];
        }
        Array.Copy(buffer, A_count, array, start, alen - A_count);
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