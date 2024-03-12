using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;

namespace BagoumLib.SignalProcessing;

public interface IBand {
    /// <summary>
    /// Beat detector containing this band.
    /// </summary>
    public BeatDetector Src { get; }
    
    /// <summary>
    /// Lower frequency limit of the band.
    /// </summary>
    public double LowLimit { get; }
    
    /// <summary>
    /// Higher frequency limit of the band.
    /// </summary>
    public double HighLimit { get; }
    
}
/// <summary>
/// Data for a frequency band that reads instantaneous power data and smears/derivates it.
/// </summary>
public record PowerBand : IObserver<double>, IBand {
    /// <summary>
    /// History of log-power signals. Note that the sampling rate of this stream is <see cref="BeatDetector.PowerSampleRate"/>.
    /// </summary>
    public CircularList<double> LogPowerHistory { get; } = new(512);
    
    public CircularList<double> SmoothLogPowerHistory { get; } = new(512);
    
    /// <summary>
    /// History of d/dt of log-power. Note that the sampling rate of this stream is <see cref="BeatDetector.PowerSampleRate"/>.
    /// </summary>
    public CircularList<double> DerivativeHistory { get; } = new(512);

    /// <summary>
    /// Size of each convolution block for power signal analysis.
    /// </summary>
    public int PowerN { get; } = 16;
    
    /// <inheritdoc/>
    public BeatDetector Src { get; }
    /// <inheritdoc/>
    public double LowLimit { get; }
    /// <inheritdoc/>
    public double HighLimit { get;  }
    
    private ChunkConvolver DSmooth { get; }
    private ChunkConvolver DGauss { get; }
    
    private readonly Event<Complex> ev = new();
    private readonly ChunkerEvent<Complex> chunker;
    public PowerBand(BeatDetector Src, double LowLimit, double HighLimit) {
        this.Src = Src;
        this.LowLimit = LowLimit;
        this.HighLimit = HighLimit;
        ev.Subscribe(x => {
            LogPowerHistory.Add(x.Real);
        });
        chunker = new(PowerN);
        ev.Subscribe(chunker);
        var sr = Src.PowerSampleRate;
        var hann = FFTHelpers.DataForFilter(i => Filters.HalfHann(i, Math.Clamp((int)(sr * 0.08), 3, PowerN - 1)), PowerN)
            .NormalizeReals();
        var dgauss = Src.FFT.ConvolveFilters(hann.ToArray(),
            FFTHelpers.DataForFilter(i => Filters.DGaussian(i, PowerN - 1, sr * 0.006) * sr, PowerN));
        //honestly i don't like smoothing here... probably because the source data is already smoothed and windowed
        DSmooth = new(Src.FFT, PowerN, hann);
        chunker.Subscribe(DSmooth);
        DSmooth.Subscribe(c => {
            for (int ii = 0; ii < c.Length; ++ii)
                SmoothLogPowerHistory.Add(c[ii].Real);
        });
        DGauss = new(Src.FFT, PowerN, dgauss);
        chunker.Subscribe(DGauss);
        DGauss.Subscribe(c => {
            for (int ii = 0; ii < c.Length; ++ii)
                DerivativeHistory.Add(c[ii].Real);
        });
        
    }

    
    /// <inheritdoc/>
    public void OnCompleted() => ev.OnCompleted();

    /// <inheritdoc/>
    public void OnError(Exception error) => ev.OnError(error);

    /// <inheritdoc/>
    public void OnNext(double value) => ev.OnNext(LogPower(value));

    /// <summary>
    /// Get the logarithm of a power. This function is adjusted to return 0 for low powers.
    /// </summary>
    public static double LogPower(double power) => 7 + Math.Log10(Math.Max(0.0000001, power));
}

/// <summary>
/// Data for a frequency band that reads amplitude signals from the time domain and smears them.
///  New data can be provided via OnNext, then read from either <see cref="History"/>
/// or <see cref="DerivativeHistory"/>.
/// </summary>
public record SignalBand : IObserver<Complex[]>, IBand {
    /// <summary>
    /// History of smeared amplitude signals.
    /// </summary>
    public CircularList<double> History { get; }
    
    /// <summary>
    /// History of amplitude derivatives.
    /// </summary>
    public CircularList<double> DerivativeHistory { get; }
    private ChunkConvolver Smear { get; }
    private ChunkConvolver DGauss { get; }
    
    /// <inheritdoc/>
    public BeatDetector Src { get; }
    /// <inheritdoc/>
    public double LowLimit { get; }
    /// <inheritdoc/>
    public double HighLimit { get;  }
    public SignalBand(BeatDetector Src, double LowLimit, double HighLimit) {
        this.Src = Src;
        this.LowLimit = LowLimit;
        this.HighLimit = HighLimit;
        History = new(Src.NHistory);
        DerivativeHistory = new(Src.NHistory);
        var sr = Src.SampleRate;
        var smear = FFTHelpers.DataForFilter(i => Filters.HalfHann(i, Math.Min(4033, (int)(sr * 0.12))), 4096).NormalizeReals();
        Smear = new(Src.FFT, Src.NSignal, smear);
        var dgLen = Math.Min(4093, (int)(sr * 0.04));
        var dgauss =
            Src.FFT.ConvolveFilters(
            FFTHelpers.DataForFilter(i => Filters.DGaussian(i, dgLen, dgLen * 0.1) * sr, 4096), 
            FFTHelpers.DataForFilter(i => Filters.HalfHann(i, Math.Min(4033, (int)(sr * 0.12))), 4096).NormalizeReals());
        ev.Subscribe(Smear);
        Smear.Subscribe(c => {
            for (int ii = 0; ii < c.Length; ++ii)
                History.Add(c[ii].Magnitude);
        });
        DGauss = new(Src.FFT, Src.NSignal, dgauss);
        ev.Subscribe(DGauss);
        //Smear.Subscribe(DGauss);
        DGauss.Subscribe(c => {
            for (int ii = 0; ii < c.Length; ++ii)
                DerivativeHistory.Add(c[ii].Real);
        });
    }

    private readonly Event<Complex[]> ev = new();
    /// <inheritdoc/>
    public void OnCompleted() => ev.OnCompleted();

    /// <inheritdoc/>
    public void OnError(Exception error) => ev.OnError(error);

    /// <inheritdoc/>
    public void OnNext(Complex[] value) => ev.OnNext(value);
}

public class BeatDetector {
    /// <summary>
    /// Sample rate of the original audio file.
    /// </summary>
    public double SampleRate { get; }
    
    /// <summary>
    /// Sampling rate of power signals. This is much lower than the raw audio sampling rate
    /// (eg. for 44100Hz audio, N=2048, this sampling rate is in the range 20-100.)
    /// </summary>
    public double PowerSampleRate => SampleRate * PowerOverlapFactor / NPower;
    
    /// <summary>
    /// Number of samples taken from the original audio stream for each <see cref="SignalBand"/> input.
    /// </summary>
    public int NSignal { get; }

    /// <summary>
    /// Width of each FFT bucket in hertz for each <see cref="SignalBand"/>.
    /// </summary>
    public double HzPerSignalBucket => SampleRate / NSignal;

    /// <summary>
    /// Number of samples taken from the original audio stream for each <see cref="PowerBand"/> input.
    /// </summary>
    public int NPower { get; } = 1024;

    /// <summary>
    /// Width of each FFT bucket in hertz for each <see cref="PowerBand"/>.
    /// </summary>
    public double HzPerPowerBucket => SampleRate / NPower;
    
    /// <summary>
    /// Number of <see cref="SignalBand"/>s.
    /// </summary>
    public int NSignalBands { get; }

    /// <summary>
    /// Number of <see cref="PowerBand"/>s.
    /// </summary>
    public int NPowerBands { get; } = 25;

    /// <summary>
    /// Where x is the amount of overlap between successive power samples, this is 1/(1-x).
    /// </summary>
    public int PowerOverlapFactor { get; } = 4;
    
    /// <summary>
    /// Number of historical records kept for energy comparison in <see cref="SignalBand"/>.
    /// </summary>
    public int NHistory { get; }
    
    /// <summary>
    /// Signal bands.
    /// </summary>
    public SignalBand[] SigBands { get; }
    
    /// <summary>
    /// Power bands.
    /// </summary>
    public PowerBand[] PowerBands { get; }
    
    /// <summary>
    /// FFT provider.
    /// </summary>
    public OouraFFT FFT { get; } = new();

    /// <summary>
    /// The index of the last read sample from the sample data for <see cref="SignalBand"/>.
    /// <br/>Note that this may be greater than any band-specific convolution data since convolutions are delayed.
    /// </summary>
    public int LastReadSignalSample { get; private set; } = 0;
    
    /// <summary>
    /// The index of the last read sample from the sample data for <see cref="PowerBand"/>.
    /// <br/>Note that this may be greater than any band-specific convolution data since convolutions are delayed.
    /// </summary>
    public int LastReadPowerSample { get; private set; } = 0;

    private readonly Complex[] sigSamples;
    private readonly Complex[] sigScratch;
    private readonly Complex[] powSamples;
    public static readonly double MaxHz = 20480;
    public static readonly double MinHz = 40;

    public BeatDetector(int audioSampleRate, int nSignal = 4096, int nSignalBands = 40, int nHistory = 65536) {
        this.SampleRate = audioSampleRate;
        this.NSignal = nSignal;
        this.sigSamples = new Complex[nSignal];
        this.sigScratch = new Complex[nSignal];
        powSamples = new Complex[NPower];
        this.NSignalBands = nSignalBands;
        this.NHistory = nHistory;
        SigBands = BandFrequencies(nSignalBands).Select(lh => new SignalBand(this, lh.low, lh.high)).ToArray();
        PowerBands = BandFrequencies(NPowerBands).Select(lh => new PowerBand(this, lh.low, lh.high)).ToArray();
    }

    private static IEnumerable<(double low, double high)> BandFrequencies(int nBands) {
        var freqs = new (double low, double high)[nBands];
        double RatioToLimit(double r) => MinHz * Math.Pow(MaxHz / MinHz, r);
        if (nBands == 25) {
            freqs[0] = (RatioToLimit(-2.0 / (nBands+2)), RatioToLimit(-1.0 /(nBands+2)));
            //band organization:
            //band1: 1->40*(2^1/3)
            //band2: 40*2^1/3 -> 40*2^4/3 (full octave)
            //band3: 40*2^4/3 -> 40*2^6/3 (2/3 octave)
            //band4: 40*2^6/3 -> 40*2^7/3, and so on
            
            //first band contains 1->40Hz by default, but we extend it to 1->40*r^1
            //second band contains 40->40*r^1 by default, but we extend it to 40*r^1->40*r^3
            for (int ii = 1; ii < nBands; ++ii) {
                var ei = ii switch {
                    1 => 1.0,
                    2 => 4,
                    _ => ii + 3
                };
                freqs[ii] = (freqs[ii-1].high, RatioToLimit(ei/(nBands+2)));
            }
        } else {
            freqs[0] = (RatioToLimit(-2.0 / (nBands-2)), RatioToLimit(-1.0 / (nBands-2)));
            for (int ii = 1; ii < nBands; ++ii) {
                var r = (ii - 1) / (nBands - 2.0);
                freqs[ii] = (freqs[ii-1].high, RatioToLimit(r));
            }
        }
        return freqs;
    }

    /// <summary>
    /// Update the beat detector with new samples.
    /// </summary>
    /// <param name="dataProvider">Provider of sample data.</param>
    /// <param name="maxIndex">Max index (exclusive) that can be sampled.</param>
    /// <param name="powers">Array in which to store the most recent power data (of length NPower).</param>
    /// <returns>True iff samples were read from the provider.</returns>
    public (bool updatedSignal, bool updatedPower) Update(IReadOnlyList<float> dataProvider, int maxIndex, double[] powers) {
        bool readSignal = false, readPower = false;
        while (LastReadSignalSample + NSignal < maxIndex) {
            for (int ii = 0; ii < NSignal; ++ii)
                sigSamples[ii] = dataProvider[LastReadSignalSample + ii];
            UpdateSignalSamples();
            LastReadSignalSample += NSignal;
            readSignal = true;
        }
        while (LastReadPowerSample + NPower/2 < maxIndex) { //center the sample
            for (int ii = 0; ii < NPower; ++ii)
                powSamples[ii] = dataProvider[LastReadPowerSample - NPower/2 + ii] * 
                                 Filters.Hann(ii - NPower / 2.0, NPower);
            UpdatePowerSamples(powers);
            LastReadPowerSample += NPower / PowerOverlapFactor;
            readPower = true;
        }
        return (readSignal, readPower);
    }

    private void UpdateSignalSamples() {
        SigBands[0].OnNext(sigSamples);
        FFT.FFTToFreq(sigSamples, true);
        int ii = 1;
        for (int ib = 1; ib < NSignalBands; ++ib) {
            Array.Clear(sigScratch, 0, sigScratch.Length);
            var band = SigBands[ib];
            for (; ii <= NSignal / 2; ++ii) {
                var freq = ii * HzPerSignalBucket;
                if (ib < NSignalBands - 1 && freq > band.HighLimit)
                    break;
                sigScratch[ii] = sigSamples[ii];
            }
            FFT.FFTFromFreq(sigScratch, true);
            //rectifier
            sigScratch.SelectInPlace(x => new(x.Magnitude, 0));
            SigBands[ib].OnNext(sigScratch);
        }
    }
    

    private void UpdatePowerSamples(double[] powers) {
        FFT.FFTToFreq(powSamples, true);
        //convert amplitudes to powers
        var sr2 = SampleRate * SampleRate;
        for (int jj = 0; jj < NPower; ++jj)
            powers[jj] = powSamples[jj].SqrMagnitude() / sr2;
        
        double allPowSum = 0;
        int ii = 1;
        for (int ib = 1; ib < NPowerBands; ++ib) {
            double powSum = 0;
            int counted = 0;
            var band = PowerBands[ib];
            for (; ii <= NPower / 2; ++ii) {
                var freq = ii * HzPerPowerBucket;
                if (ib < NSignalBands - 1 && freq > band.HighLimit)
                    break;
                powSum += powers[ii];
                ++counted;
            }
            allPowSum += powSum;
            //We treat the power of a frequency bin as the sum of frequency powers (amp^2), adjusted
            // for the actual number of elements in the bin.
            var adjustedPowSum = (counted == 0) ?
                0 :
                powSum * (band.HighLimit - band.LowLimit) / (counted * HzPerPowerBucket);
            PowerBands[ib].OnNext(adjustedPowSum);
        }
        PowerBands[0].OnNext(allPowSum);
    }

    
    
    /*
    private (double average, double variance) ComputeHistoryStats(int band) {
        double avg = 0;
        for (int ii = 0; ii < Bands.Length; ++ii) 
            avg += Bands[ii][band].TotalEnergy;
        avg /= Bands.Length;
        double variance = 0;
        for (int ii = 0; ii < Bands.Length; ++ii) {
            var e = Bands[ii][band].TotalEnergy;
            variance += (e - avg) * (e - avg);
        }
        return (avg, variance / Bands.Length);
    }*/
    
    private static double GainForFrequency(double freq) {
        var table = itur468;
        if (freq < table[0].freq)
            return table[0].gain;
        for (int ii = 1; ii < table.Length; ++ii) {
            var (f, g) = table[ii];
            if (freq <= f) {
                return BMath.Lerp(table[ii - 1].gain, g, BMath.Ratio(table[ii - 1].freq, f, freq));
            }
        }
        return table[^1].gain;
    }
    
    private static readonly (double freq, double gain)[] aweight = {
        (25,-44.8), (31.5, -39.5), (40, -34.5), (50, -30.3), (63, -26.2), (80, -22.4), (100,-19.1),(125,-16.2),
        (160,-13.2),(200,-10.8),(250,-8.7),(315,-6.6),(400,-4.8),(500,-3.2),(630,-1.9),(800,-0.8),
        (1000,0),(1250,0.6),(1600,1),(2000,1.2),(2500,1.3),(3150,1.2),(4000,1.0),(5000,0.6),
        (6300,-0.1),(8000,-1.1),(10000,-2.5),(12500,-4.3),(16000,-6.7),(20000,-9.3)
    };
    private static readonly (double freq, double gain)[] itur468 = {
        (31.5, -29.9), (63, -23.9), (100, -19.8), (200, -13.8), (400, -7.8),
        (800, -1.9), (1000, 0), (2000, 5.6), (3150, 9), (4000, 10.5), (5000, 11.7),
        (6300, 12.2), (7100, 12), (8000, 11.4), (9000, 10.1), (10000, 8.1), (12500, 0),
        (14000, -5.3), (16000, -11.7), (20000, -22.2), (31500, -42.7)
    };
}
