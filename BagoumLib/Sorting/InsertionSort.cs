using System.Collections.Generic;

namespace BagoumLib.Sorting {
public class InsertionSort<T> {
    public void Sort(T[] array, int start, int end, IComparer<T> comp) => Sort_(array, start, end, comp);
    public static void Sort_(T[] array, int start, int end, IComparer<T> comp) {
        for (int i = start + 1; i < end; i++) {
            T temp = array[i];
            int j;
            for (j = i; j > start && comp.Compare(temp, array[j - 1]) < 0; j--)
                array[j] = array[j - 1];
            array[j] = temp;
        }
    }
}
}