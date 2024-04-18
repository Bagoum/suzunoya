using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Tasks;
using JetBrains.Annotations;
using static System.Math;
namespace BagoumLib.SignalProcessing;

/// <summary>
/// An algorithm providing an FFT implementation.
/// </summary>
public interface IFFT {
    /// <summary>
    /// Given a set of data in bit-reversed order,
    /// find the FFT of that data in proper order in-place.
    /// <br/>Note that sign=-1 is the forward FFT and sign=1 is the inverse FFT.
    /// </summary>
    void DoFFT(Complex[] data, int sign);
}

/// <summary>
/// FFT implementation based on Takuya Ooura's FFT library.
/// </summary>
public record OouraFFT : IFFT {
    private static readonly Dictionary<int, FastFourierTransform> holders = new();

    /// <inheritdoc/>
    public void DoFFT(Complex[] data, int sign) {
        var N = data.Length;
        if (!holders.TryGetValue(N, out var h))
            holders[N] = h = new FastFourierTransform(N);
        if (sign < 0)
            h.Forward(data);
        else
            h.Inverse(data);
    }
}

/// <summary>
/// Comb-based implementation of FFT.
/// (Demonstration purposes only; this is significantly slower than <see cref="OouraFFT"/>.)
/// </summary>
internal record CombFFT: IFFT {
    /// <inheritdoc/>
    public void DoFFT(Complex[] data, int sign) {
        FFTHelpers.BitReverseIndices(data);
        var N = data.Length;
        for (int comb = 1; comb < N; comb *= 2) {
            var root = FFTHelpers.IExp(sign * PI / comb);
            for (int evens = 0; evens < N; evens += comb * 2) {
                var odds = evens + comb;
                var accroot = root;
                (data[evens], data[odds]) = (data[evens] + data[odds], data[evens] - data[odds]);
                for (int k = 1; k < comb; ++k) {
                    var p = data[evens + k];
                    var q = data[odds + k] * accroot;
                    data[evens + k] = p + q;
                    data[odds + k] = p - q;
                    accroot *= root;
                }
            }
        }
    }
}

/// <summary>
/// Recursion-based implementation of FFT.
/// (Demonstration purposes only; this is significantly slower than <see cref="OouraFFT"/>.)
/// </summary>
internal record RecursiveFFT : IFFT {
    /// <inheritdoc/>
    public void DoFFT(Complex[] data, int sign) {
        FFTHelpers.BitReverseIndices(data);
        void InnerStep(int start, int N) {
            if (N <= 1) return;
            InnerStep(start, N / 2);
            InnerStep(start + N / 2, N / 2);
            var root = FFTHelpers.IExp(sign * 2 * PI / N);
            var accroot = new Complex(1, 0);
            for (int ii = 0; ii < N / 2; ++ii) {
                var p = data[start + ii];
                var q = data[start + N / 2 + ii] * accroot;
                data[start + ii] = p + q;
                data[start + N / 2 + ii] = p - q;
                accroot *= root;
            }
        }
        InnerStep(0, data.Length);
    }
}


/// <summary>
/// Helper functions for Fast Fourier Transforms.
/// </summary>
[PublicAPI]
public static class FFTHelpers {
    /// <summary>
    /// Divide each element by the length of the array. Required for FFT normalization.
    /// </summary>
    public static Complex[] DivideByN(this Complex[] values) {
        var N = values.Length;
        for (int ii = 0; ii < N; ++ii) {
            values[ii] *= 1.0 / N;
        }
        return values;
    }

    /// <summary>
    /// Replace each element in the array with its complex conjuate.
    /// </summary>
    public static Complex[] Conjugate(this Complex[] values) {
        for (int ii = 0; ii < values.Length; ++ii)
            values[ii] = new(values[ii].Real, -values[ii].Imaginary);
        return values;
    }

    /// <summary>
    /// Perform an in-place FFT from a sequence of values into a set of frequencies.
    /// </summary>
    public static Complex[] FFTToFreq(this IFFT fft, Complex[] values, bool dealias = false) {
        fft.DoFFT(values, -1);
        if (dealias)
            RemoveAliasing(values);
        return values;
    }

    /// <summary>
    /// Remove aliased frequencies in the second half of the array (frequencies greater than the folding frequency).
    /// <br/>Since DFT is symmetric around the folding frequency, we assume that the true frequencies are the lower
    ///  frequencies and the higher frequencies are aliasing artifacts.
    /// </summary>
    public static Complex[] RemoveAliasing(this Complex[] frequencies) {
        var N = frequencies.Length;
        for (int ii = 1; ii < N / 2; ++ii)
            frequencies[ii] *= 2;
        for (int ii = N / 2 + 1; ii < N; ++ii)
            frequencies[ii] = Complex.Zero;
        return frequencies;
    }

    /// <summary>
    /// Re-add aliased frequencies removed by <see cref="RemoveAliasing"/>.
    /// </summary>
    public static Complex[] AddAliasing(this Complex[] frequencies) {
        var N = frequencies.Length;
        for (int ii = 1; ii < N / 2; ++ii) {
            var c = frequencies[ii];
            frequencies[ii] = new(c.Real / 2, c.Imaginary / 2);
            frequencies[N - ii] = new(c.Real / 2, c.Imaginary / -2);
        }
        return frequencies;
    }
    
    /// <summary>
    /// Perform an in-place FFT from a set of frequencies into a sequence of values. Also divide by N.
    /// </summary>
    public static Complex[] FFTFromFreq(this IFFT fft, Complex[] data, bool realias=false) {
        if (realias)
            AddAliasing(data);
        fft.DoFFT(data, 1);
        return data.DivideByN();
    }

    /// <summary>
    /// Normalizes the real components only of a data stream in-place, so that the real components to 1.
    /// </summary>
    public static Complex[] NormalizeReals(this Complex[] data) {
        double total = 0;
        for (int ii = 0; ii < data.Length; ++ii)
            total += data[ii].Real;
        total = 1 / total;
        for (int ii = 0; ii < data.Length; ++ii)
            data[ii] = new(data[ii].Real * total, 0);
        return data;
    }

    /// <summary>
    /// Normalize the values of a data stream in-place, such that their magnitudes sum to 1.
    /// </summary>
    public static Complex[] Normalize(this Complex[] data) {
        double total = 0;
        for (int ii = 0; ii < data.Length; ++ii)
            total += data[ii].Magnitude;
        total = 1 / total;
        for (int ii = 0; ii < data.Length; ++ii)
            data[ii] = new(data[ii].Real * total, data[ii].Imaginary * total);
        return data;
    }
    

    /// <summary>
    /// Normalizes a vector such that the square of square magnitudes sums to 1.
    /// </summary>
    public static Complex[] NormalizeEnergy(this Complex[] data) {
        double total = 0;
        for (int ii = 0; ii < data.Length; ++ii)
            total += data[ii].SqrMagnitude();
        if (total > 0) {
            total = 1 / Math.Sqrt(total);
            for (int ii = 0; ii < data.Length; ++ii)
                data[ii] *= total;
        }
        return data;
    }
    
    /// <summary>
    /// Create an array of complex numbers whose values are the values of `fn` evaluated between 0 and `period`.
    /// </summary>
    public static Complex[] DataForFnOverPeriod(Func<double, Complex> fn, double period, int samples) {
        var data = new Complex[samples];
        for (int ii = 0; ii < samples; ++ii)
            data[ii] = fn(period * ii / samples);
        return data;
    }
    
    /// <summary>
    /// Create an array of complex numbers whose values are values of `fn` evaluated between 0 and `samples/samplingRate`.
    /// </summary>
    public static Complex[] DataForFnAtRate(Func<double, Complex> fn, double samplingRate, int samples) {
        var data = new Complex[samples];
        for (int ii = 0; ii < samples; ++ii)
            data[ii] = fn(ii / samplingRate);
        return data;
    }
    
    /// <summary>
    /// Create an array of complex numbers whose values are the values of `fn` evaluated between 0 and `samples`.
    /// </summary>
    public static Complex[] DataForFn(Func<int, Complex> fn, int samples) {
        var data = new Complex[samples];
        for (int ii = 0; ii < samples; ++ii)
            data[ii] = fn(ii);
        return data;
    }
    
    /// <summary>
    /// Remap the range [ptN/2, ptN) to [-ptN/2,0).
    /// </summary>
    public static int WrapIndex(int i, int ptN) {
        return i >= ptN / 2  ? i - ptN : i;
    }
    
    /// <summary>
    /// Create an array of complex numbers whose values are the values of `fn` evaluated between 0 and `samples`,
    /// and the indices are passed to the provided function as follows:
    /// <br/>0 1 2 3... (N/2-2) (N/2-1) -N/2 -N/2+1 ... -3 -2 -1
    /// <br/>ie. The first half of the array is nonnegative, and the second half is negative.
    /// <br/>This function should be used for the filters in <see cref="Filters"/>.
    /// </summary>
    public static Complex[] DataForFilter(Func<int, Complex> fn, int samples) {
        var data = new Complex[samples];
        for (int ii = 0; ii < samples; ++ii)
            data[ii] = fn(WrapIndex(ii, samples));
        return data;
    }

    /// <summary>
    /// Convolve two periodic sequences in the time domain together.
    /// <br/>The arrays must be the same length, and this length must be a power of two.
    /// <br/>Overwrites both arrays and returns data1.
    /// <br/>Note that convolution is commutative (Convolve(a,b) = Convolve(b,a)).
    /// </summary>
    public static Complex[] Convolve(this IFFT fft, Complex[] data1, Complex[] data2) {
        if (data1.Length != data2.Length)
            throw new Exception("Convolution targets must be of same length");
        fft.FFTToFreq(data1);
        fft.FFTToFreq(data2);
        for (int ii = 0; ii < data1.Length; ++ii)
            data1[ii] *= data2[ii];
        fft.FFTFromFreq(data1);
        return data1;
    }
    
    /// <summary>
    /// Correlates two periodic sequences in the time domain together.
    /// <br/>The arrays must be the same length, and this length must be a power of two.
    /// <br/>Overwrites both arrays and returns data1.
    /// <br/>Note that Correlate(x, y) = Convolve(x, Conjugate(y)).
    /// Correlation is not commutative, but convolution is.
    /// </summary>
    public static Complex[] Correlate(this IFFT fft, Complex[] data1, Complex[] data2) {
        if (data1.Length != data2.Length)
            throw new Exception("Convolution targets must be of same length");
        fft.FFTToFreq(data1);
        fft.FFTToFreq(data2);
        for (int ii = 0; ii < data1.Length; ++ii)
            //pointwise multiply complex conjugate
            data1[ii] *= new Complex(data2[ii].Real, -data2[ii].Imaginary);
        fft.FFTFromFreq(data1);
        return data1;
    }

    /// <summary>
    /// Get the forward and backward length of a response filter.
    /// <br/>Note: for this to work, elements in the middle of the array must be zeroed out correctly.
    /// </summary>
    public static (int forward, int backward) GetFilterLength(this Complex[] resp) {
        var forward = (resp.Length - 1) / 2;
        for (; forward > 0; --forward)
            if (resp[forward].Magnitude > 0)
                break;
        var backward = resp.Length / 2;
        for (; backward > 0; --backward)
            if (resp[^backward].Magnitude > 0)
                break;
        return (forward, backward);
    }

    /// <summary>
    /// Pad a response filter in the center so its length is `reqLen`. (If the filter is already of this length,
    ///  then does nothing.)
    /// </summary>
    public static Complex[] PadFilterToLength(this Complex[] resp, int reqLen) {
        if (resp.Length >= reqLen)
            return resp;
        return resp.Pad(0, reqLen - resp.Length, 0);
    }

    /// <summary>
    /// Convolve two response filters together. If they are not of the same size, will pad them in the center.
    /// <br/>Convolving two response filters of size N and M results in a filter of size N+M. If this size
    ///  is greater than the size of the array, then pollution will occur. To avoid this, this function
    ///  will check the filter sizes and create a longer array if required. It will also zero out
    ///  elements in the middle of the result array after convolution.
    /// <br/>May overwrite both arrays and return data1. Alternatively, may return a longer array.
    /// </summary>
    public static Complex[] ConvolveFilters(this IFFT fft, Complex[] resp1, Complex[] resp2) {
        var maxLen = Math.Max(resp1.Length, resp2.Length);
        var (f1, b1) = resp1.GetFilterLength();
        var (f2, b2) = resp2.GetFilterLength();
        var f = f1 + f2;
        var b = b1 + b2;
        while (f >= maxLen/2-1 || b >= maxLen/2 || f + b >= maxLen - 1)
            maxLen *= 2;
        resp1 = resp1.PadFilterToLength(maxLen);
        resp2 = resp2.PadFilterToLength(maxLen);
        fft.Convolve(resp1, resp2);
        for (int ii = f + 1; ii <= (maxLen - 1) / 2; ++ii)
            resp1[ii] = default;
        for (int ii = b + 1; ii <= maxLen / 2; ++ii)
            resp1[^ii] = default;
        return resp1;
    }
    
    /// <summary>
    /// Convolve two periodic sequences together, where the first is in the time domain
    ///  and the second is in the frequency domain.
    /// <br/>The arrays must be the same length, and this length must be a power of two.
    /// <br/>Overwrites and returns data1.
    /// </summary>
    public static Complex[] ConvolveOnFreq(this IFFT fft, Complex[] data1, Complex[] fdata2) {
        if (data1.Length != fdata2.Length)
            throw new Exception("Convolution targets must be of same length");
        fft.FFTToFreq(data1);
        for (int ii = 0; ii < data1.Length; ++ii)
            data1[ii] *= fdata2[ii];
        fft.FFTFromFreq(data1);
        return data1;
    }

    /// <summary>
    /// Pad an array of data with default values on the left, middle, and right.
    /// </summary>
    public static T[] Pad<T>(this T[] data, int left, int center, int right) {
        var res = new T[data.Length + left + center + right];
        //Put zeroes in the middle of the response function
        for (int ii = 0; ii < data.Length; ++ii) {
            if (ii <= (data.Length - 1) / 2)
                res[left + ii] = data[ii];
            else
                res[^(right + data.Length - ii)] = data[ii];
        }
        return res;
    }

    public static async Task Push<T>(this IAsyncEnumerable<T> src, IObserver<T> into) {
        await foreach (var x in src)
            into.OnNext(x);
        into.OnCompleted();
    }
    
    /// <summary>
    /// Do a pointwise multiplication of two arrays, storing the output in-place in the first array.
    /// <br/>If these arrays are the output of a forward FFT, then this represents a convolution.
    /// </summary>
    public static Complex[] PointwiseMultiply(this Complex[] a, Complex[] b) {
        for (int ii = 0; ii < a.Length; ++ii)
            a[ii] *= b[ii];
        return a;
    }

    /// <summary>
    /// Return true if x is a power of two.
    /// </summary>
    public static bool IsPowerOfTwo(int x) =>
        x != 0 && (x & (x - 1)) == 0;

    /// <summary>
    /// Return the first power of 2 greater than or equal to x.
    /// </summary>
    public static int NextPowerOfTwo(int x) {
        if (IsPowerOfTwo(x)) return x;
        int power = 4;
        while (power < x)
            power *= 2;
        return power;
    }
    
    
    /// <summary>
    /// Reorder the elements of the array by placing each element at its bit-reversed index.
    /// </summary>
    public static void BitReverseIndices(Complex[] arr) {
        if (!IsPowerOfTwo(arr.Length))
            throw new Exception("Array length must be a power of two for bit-reversing indices");
        var bits = (int) Math.Log(arr.Length, 2);
        for (int ii = 0; ii < arr.Length; ++ii) {
            var ri = BitReverse(ii, bits);
            if (ii < ri) {
                (arr[ii], arr[ri]) = (arr[ri], arr[ii]);
            }
        }
    }
    
    /// <summary>
    /// Reverse the last `bits` bits of the integer `x`.
    /// <br/>eg. BitReverse(3, 4) = BitReverse(0b0011, 4) = 0b1100 = 12
    /// </summary>
    public static byte BitReverse(int x, int bits) {
        int result = 0;
        for (int i = 0; i < bits; i++)
            if ((x & (1 << i)) != 0)
                result |= 1 << (bits - 1 - i);
        return (byte)result;
    }

    /// <summary>
    /// Return e^(ix).
    /// </summary>
    public static Complex IExp(double x) => 
        new(Math.Cos(x), Math.Sin(x));

}