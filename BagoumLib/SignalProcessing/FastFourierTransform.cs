//Original implementation from FFTFlat https://github.com/sinshu/fftflat/tree/main, distributed under MIT.
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace BagoumLib.SignalProcessing;

/// <summary>
/// Performs fast Fourier transform (FFT).
/// </summary>
public sealed class FastFourierTransform {
    /// <summary>
    /// The length of the FFT.
    /// </summary>
    public int Length { get; }

    private readonly int[] bitReversal;
    private readonly double[] trigTable;
    private readonly double inverseScaling;

    /// <summary>
    /// Initializes the FFT with the given length.
    /// </summary>
    /// <param name="length">The length of the FFT.</param>
    /// <remarks>
    /// The FFT length must be a power of two.
    /// </remarks>
    public FastFourierTransform(int length) {
        if (!FFTHelpers.IsPowerOfTwo(length)) {
            throw new ArgumentException("The FFT length must be a power of two.", nameof(length));
        }

        this.Length = length;
        this.bitReversal = new int[3 + (int)(Math.Sqrt(length) + 0.001)];
        this.trigTable = new double[length / 2];
        this.inverseScaling = 1.0 / length;
    }

    /// <summary>
    /// Performs forward FFT in-place.
    /// </summary>
    /// <param name="samples">The samples to be transformed.</param>
    public unsafe void Forward(Span<Complex> samples) {
        if (samples.Length != Length) {
            throw new ArgumentException("The length of the span must match the FFT length.", nameof(samples));
        }

        fixed (Complex* a = samples)
        fixed (int* ip = bitReversal)
        fixed (double* w = trigTable) {
            fftsg.cdft(2 * Length, -1, (double*)a, ip, w);
        }
    }

    /// <summary>
    /// Performs inverse FFT in-place.
    /// </summary>
    /// <param name="spectrum">The spectrum to be transformed.</param>
    public unsafe void InverseScaled(Span<Complex> spectrum) {
        Inverse(spectrum);
        ArrayMath.MultiplyInplace(MemoryMarshal.Cast<Complex, double>(spectrum), inverseScaling);
    }


    /// <summary>
    /// Performs inverse FFT in-place.
    /// </summary>
    /// <param name="spectrum">The spectrum to be transformed.</param>
    public unsafe void Inverse(Span<Complex> spectrum) {
        if (spectrum.Length != Length) {
            throw new ArgumentException("The length of the span must match the FFT length.", nameof(spectrum));
        }

        fixed (Complex* a = spectrum)
        fixed (int* ip = bitReversal)
        fixed (double* w = trigTable) {
            fftsg.cdft(2 * Length, 1, (double*)a, ip, w);
        }
    }
}