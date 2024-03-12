using System;
using System.Numerics;
using BagoumLib.Mathematics;
using static System.Math;

namespace BagoumLib.SignalProcessing;

/// <summary>
/// Reference filters for audio processing.
/// </summary>
public static class Filters {
    /// <summary>
    /// Hann windowing over the range [-(N-1)/2, (N-1)/2]. Any outside elements will be zeroed.
    /// </summary>
    public static double Hann(double i, int N) {
        var lim = (N - 1) / 2.0;
        return (i < -lim || i > lim) ? 0 :
            0.5 + 0.5 * Cos(Math.PI * i / lim);
    }
    
    /// <summary>
    /// Normalized half-Hann windowing over the range [0, N/2). Any outside elements will be zeroed.
    /// </summary>
    public static double HalfHann(double i, int N) {
        var lim = N / 2; //round odds down
        return (i < 0 || i >= lim) ? 0 :
            0.5 + 0.5 * Cos(Math.PI * i / lim);
    }

    /// <summary>
    /// A filter that returns the same source data.
    /// </summary>
    public static double Identity(int i) => (i == 0) ? 1 : 0;

    /// <summary>
    /// The symmetric Tukey window over the range [-(N-1)/2,(N-1)/2]. Any outside elements will be zeroed.
    /// <br/>The tail `falloff` ratios have a cosine lobe, and the center area is 1.
    /// </summary>
    /// <param name="i">Sample index in the range [-N/2,N/2).</param>
    /// <param name="N">Total number of samples. Should be odd.</param>
    /// <param name="falloff">Falloff ratio (in the range [0,1]).</param>
    public static double Tukey(int i, int N, double falloff) {
        var nf = N * falloff / 2;
        // ReSharper disable once PossibleLossOfFraction
        var x0 = Math.Abs(i) - ((N - 1) / 2) + nf;
        if (x0 <= 0)
            return 1;
        if (x0 >= nf)
            return 0;
        return 0.5 + 0.5 * Cos(PI * x0 / nf);
    }

    private static readonly double sqrtTau = Math.Sqrt(2 * Math.PI);
    
    /// <summary>
    /// Gaussian filter centered at 0. (Has an inbuilt Tukey filter to zero out elements outside the range.)
    /// </summary>
    /// <param name="i">Sample index in the range [-N/2,N/2).</param>
    /// <param name="N">Total number of samples for the filter duration. Should be odd.</param>
    /// <param name="sigma">Standard deviation in number of samples. For best results, should be less than N/8.</param>
    public static double Gaussian(int i, int N, double sigma) {
        return Math.Pow(Math.E, -0.5 * i * i / (sigma * sigma))
               / (sigma * sqrtTau)
               * Tukey(i, N, 0.1);
    }
    
    /// <summary>
    /// Derivative Gaussian filter centered at 0. (Has an inbuilt Tukey filter to zero out elements outside the range.)
    /// <br/>Note that this is a d/di filter (derivate w.r.t sample index).
    /// <br/>For a d/dt filter (derivative w.r.t time), multiply the output of this function by
    /// the sampling rate (=di/dt).
    /// </summary>
    /// <param name="i">Sample index in the range [-N/2,N/2).</param>
    /// <param name="N">Total number of samples for the filter duration. Should be odd.</param>
    /// <param name="sigma">Standard deviation in number of samples. For best results, should be less than N/8.</param>
    public static double DGaussian(int i, int N, double sigma) {
        return Math.Pow(Math.E, -0.5 * i * i / (sigma * sigma)) * -i
               / (sigma * sigma * sigma * sqrtTau)
                * Tukey(i, N, 0.1);
    }
    
    
    
    /// <summary>
    /// Low-pass filter.
    /// </summary>
    /// <param name="i">Sample index in the range [-N/2,N/2).</param>
    /// <param name="N">Number of samples for the filter duration. Should be odd.</param>
    /// <param name="cutoff">Frequency cutoff as a fraction of sampling rate. Has a rolloff of 4*SR/N Hz.</param>
    /// <returns></returns>
    public static double LowPass(int i, int N, double cutoff) {
        //Based on https://tomroelandts.com/articles/how-to-create-a-simple-low-pass-filter
        //However, we center at x=0 and use wrap-around indices to avoid a delay.
        return 2 * cutoff * BMath.Sinc(2.0 * cutoff * i) * Hann(i, N);
    }
    
    /// <summary>
    /// High-pass filter.
    /// </summary>
    /// <param name="i">Sample index [0, ptN).</param>
    /// <param name="N">Number of samples for the filter duration. Should be odd.</param>
    /// <param name="cutoff">Frequency cutoff as a fraction of sampling rate. Has a rolloff of 4*SR/N Hz.</param>
    /// <returns></returns>
    public static double HighPass(int i, int N, double cutoff) {
        //Based on https://tomroelandts.com/articles/how-to-create-a-simple-high-pass-filter
        return (i == 0) ?
            1 - LowPass(i, N, cutoff) :
            -LowPass(i, N, cutoff);
    }

    /// <summary>
    /// Band-pass filter.
    /// </summary>
    /// <param name="fft">FFT provider.</param>
    /// <param name="N">Number of samples for the filter duration. Should be odd.</param>
    /// <param name="ptN">Total number of samples to put in the array. Should be a value 2^k >= N.</param>
    /// <param name="cutoff1">Lower frequency cutoff as a fraction of sampling rate.</param>
    /// <param name="cutoff2">Higher frequency cutoff as a fraction of sampling rate.</param>
    /// <returns></returns>
    public static Complex[] BandPass(this IFFT fft, int N, int ptN, double cutoff1, double cutoff2) {
        var low = FFTHelpers.DataForFilter(i => Filters.LowPass(i, N, cutoff2), ptN);
        var high = FFTHelpers.DataForFilter(i => Filters.HighPass(i, N, cutoff1), ptN);
        var merged = fft.Convolve(low, high);
        for (int ii = (N+1)/2; ii < ptN - (N-1)/2; ++ii)
            merged[ii] = Complex.Zero;
        return merged;
    }
    
    
}