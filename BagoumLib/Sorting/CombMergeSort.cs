using System;
using System.Collections.Generic;

namespace BagoumLib.Sorting {

/// <summary>
/// Merge sort that uses a bottom-up combing method.
/// <br/>Best for when the array is mostly sorted.
/// </summary>
public class CombMergeSorter<T> : ISorter<T> {
    /// <summary>
    /// Sort an array using bottom-up mergesort.
    /// </summary>
    /// <param name="array">Array to sort</param>
    /// <param name="start">Starting index for sort (inclusive)</param>
    /// <param name="end">Ending index for sort (exclusive)</param>
    /// <param name="comp">Sort comparer</param>
    /// <param name="buffer">Working buffer. Must have at least (end-start)/2 capacity</param>
    public static void Sort(T[] array, int start, int end, LeqCompare<T> comp, T[] buffer) {
        var len = end - start;
        for (var comb = 1; comb < len; comb *= 2) {
            //Comb backwards so the first half of the merge (A) is always the shorter half
            //This allows efficient buffer usage
            var minA = start + comb;
            for (var b = end - comb; b > start; b -= comb * 2) {
                //Optimization for mostly-sorted case
                if (comp(in array[b - 1], in array[b]))
                    continue;
                var a = (minA > b ? minA : b) - comb;
                var bi = b;
                var e = b + comb;
                var a1i = 0;
                var a2i = 0;
                var a3i = a;
                //Merge [a, b) and [b, e)
                //array[bi, b+comb) are remaining elements from B
                //buffer[a1i, a2i) are elements from A, succeeded by array[a3i, b)
                //array[start, a3i) are merged elements
                
                //a3i == e means that we have merged everything
                while (a3i < e) {
                    //If there are no B elements remaining,
                    // then copy A elements to the end of the merged elements and we are done
                    //Since comb=len(B)>=len(A), all A elements must be in the buffer
                    // a3i-start = len(B)+k, where k is the number of merged elements from A
                    if (bi == e) {
                        Array.Copy(buffer, a1i, array, a3i, a2i - a1i);
                        break;
                    }
                    if (a2i > a1i) {
                        //Use A from buffer
                        if (b > a3i)
                            //If the element at the index-to-merge is A (as opposed to freed space from B),
                            // move it to the buffer
                            buffer[a2i++] = array[a3i];
                        array[a3i++] = 
                            comp(in buffer[a1i], in array[bi]) ?
                                buffer[a1i++] :
                                array[bi++];
                    } else if (b > a3i) {
                        //A-buffer is empty, use A in array
                        if (comp(in array[a3i], in array[bi])) {
                            //A is smaller, no swap required
                            a3i++;
                        } else {
                            //Move A to buffer and use element from B
                            buffer[a2i++] = array[a3i];
                            array[a3i++] = array[bi++];
                        }
                    } else
                        //If there are no A elements remaining, then the B elements are already sorted, we are done
                        break;
                }

            }
        }
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