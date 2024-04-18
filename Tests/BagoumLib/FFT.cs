using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.SignalProcessing;
using BagoumLib.Tasks;
using NAudio.Wave;
using NUnit.Framework;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using static BagoumLib.SignalProcessing.FFTHelpers;
using static System.Math;
using static BagoumLib.IEnumExtensions;

namespace Tests.BagoumLib;

public class FFT {
    private static readonly IFFT[] providers = {
        new OouraFFT(), new CombFFT(), new RecursiveFFT(),
    };
    private static readonly Random rng = new();
    private static readonly OouraFFT fft = new();

    private double Sinc(double x) => x == 0 ? 1 : Sin(PI * x) / (PI * x);

    private double Rect(double x) => x switch {
        > 0.5 => 0,
        < -0.5 => 0,
        _ => 1
    };

    private Func<double, double> WrapAround(Func<double, double> f, double p) => 
        x => f(BMath.Mod(p, x - p/2));
    private double WrapAround(double x, double p) => BMath.Mod(p, x - p/2);

    private Signal GraphRealSignals(Plot spec, double sr, IEnumerable<Complex> signals, int? actualSamples = null, double xOffset = 0, Maybe<Color>? color = null,
            IXAxis? xAxis = null, IYAxis? yAxis = null) {
        var cl = color.HasValue ? color.Value.ValueOrSNull() : Colors.Navy;
        var data = signals.Select(x => x.Real).ToArray();
        var time = spec.Add.Signal(data, 1/sr, cl);
        //time.LineStyle.IsVisible = false;
        time.Data.XOffset = xOffset;
        time.Axes.YAxis = yAxis ??= spec.Axes.Left;
        time.Axes.XAxis = xAxis ??= spec.Axes.Bottom;
        xAxis.Label.Text = "Time";
        xAxis.Min = 0;
        xAxis.Max = xOffset + (actualSamples ?? data.Length) / sr;
        yAxis.Label.Text = "Signal Amplitude";
        if (cl.Try(out var c)) {
            time.LineStyle.Color = c;
            xAxis.Label.ForeColor = c;
            yAxis.Label.ForeColor = c;
        }
        return time;
    }
    private SignalXY GraphFrequencies(Plot spec, double sr, Complex[] freqs) {
        var c = Colors.Crimson;
        var freqXs = freqs.Length.Range().Select(i => Math.Log10(1 + i * sr / freqs.Length)).ToArray();
        var freq = spec.Add.SignalXY(freqXs, freqs.Select(x => x.Magnitude).ToArray(), c);
        var freqR = spec.Add.SignalXY(freqXs, freqs.Select(x => Abs(x.Real)).ToArray(), Colors.Orange);
        var freqI = spec.Add.SignalXY(freqXs, freqs.Select(x => Abs(x.Imaginary)).ToArray(), Colors.DarkGoldenRod);
        freqR.LineStyle.Pattern = freqI.LineStyle.Pattern = LinePattern.Dashed;
        freq.Axes.YAxis = freqR.Axes.YAxis = freqI.Axes.YAxis = spec.Axes.Right;
        freq.Axes.XAxis = freqR.Axes.XAxis = freqI.Axes.XAxis = spec.Axes.Top;
        spec.Axes.Top.Label.Text = "Frequency";
        spec.Axes.Top.Label.ForeColor = c;
        //spec.Axes.Top.Min = Math.Log10(40);
        //spec.Axes.Top.Max = Math.Log10(sr / 2);
        spec.Axes.Top.TickGenerator = new NumericAutomatic() {
            MinorTickGenerator = new LogMinorTickGenerator() { Divisions = 10 },
            LabelFormatter = f => $"{Math.Pow(10, f) - 1:N1}"
        };
        spec.Axes.Right.Label.Text = "Frequency Amplitude";
        spec.Axes.Right.Label.ForeColor = c;
        spec.Axes.Right.Max = freqs.Max(x => x.Magnitude);
        spec.Axes.Right.Min = 0;
        return freq;
    }
    private Plot CreateFFTGraph(double sr, Complex[] data, int? actualSamples = null) {
        //In blue dots: the provided data's real components.
        //In red line: the frequency spectrum of the provided data.
        //In green line: the inverse FFT of the frequency spectrum. This should overlap the blue dots.
        var fft = new OouraFFT();
        Plot spec = new();
        GraphRealSignals(spec, sr, data, actualSamples);
        fft.FFTToFreq(data, true);
        GraphFrequencies(spec, sr, data);
        fft.FFTFromFreq(data, true);
        var replotTime = spec.Add.Signal(data.Select(x => x.Real).ToArray(), 1/sr, Colors.ForestGreen);
        replotTime.Marker.IsVisible = false;
        replotTime.Axes.YAxis = spec.Axes.Left;
        replotTime.Axes.XAxis = spec.Axes.Bottom;
        return spec;
    }

    [Test]
    public void FFTonFFT() {
        var N = 512;
        var plt = new Plot();
        var fft = new OouraFFT();
        var data1 = DataForFn(i => Filters.Tukey(i - 200, 240, 0.2) + 0.4 * Filters.Hann(i - 140, 70), N).NormalizeReals();
        var l1 = GraphRealSignals(plt, 1, data1, color: Colors.ForestGreen);
        l1.Label = "Rectangle";
        var fdata1 = fft.FFTToFreq(data1);
        var l2 = GraphRealSignals(plt, 1, fdata1, xAxis: plt.Axes.Top, yAxis:plt.Axes.Right);
        l2.Label = "FFT";
        fft.FFTToFreq(fdata1).DivideByN();
        var l3 = GraphRealSignals(plt, 1, fdata1);
        l3.LineStyle.Color = Colors.DarkRed;
        l3.Label = "FFT * FFT";
        plt.ShowLegend();
        plt.SavePng("../../../../../doublefft.png", 3200, 700);
    }

    [Test]
    public async Task Correlation() {
        var N = 512;
        var plt = new Plot();
        var data1 = DataForFn(i => Filters.Tukey(i - 200, 240, 0.2) + 0.4 * Filters.Hann(i - 140, 70), N).NormalizeReals();
        var l1 = GraphRealSignals(plt, 1, data1, color: Colors.ForestGreen);
        l1.LineStyle.Pattern = LinePattern.Dashed;
        l1.Label = "Rectangle";
        var data2 = DataForFn(i => i < 60 ? 0 : Filters.HalfHann(i - 60, 240), N).NormalizeReals();
        var l2 = GraphRealSignals(plt, 1, data2, color: Colors.Chocolate);
        l2.LineStyle.Pattern = LinePattern.Dashed;
        l2.Label = "Triangle";
        var fft = new OouraFFT();
        var conv = fft.Convolve(data2.ToArray(), data1.ToArray());
        var l3 = GraphRealSignals(plt, 1, conv, color: Colors.Black);
        l3.LineStyle.Pattern = LinePattern.DenselyDashed;
        l3.Label = "Convolution (symmetric)";
        var corr = fft.Correlate(data1.ToArray(), data2.ToArray());
        var l4 = GraphRealSignals(plt, 1, corr, color: Colors.MidnightBlue);
        l4.Label = "Correlation RxT";
        var corr2 = fft.Correlate(data2.ToArray(), data1.ToArray());
        var l5 = GraphRealSignals(plt, 1, corr2, color: Colors.DarkGoldenRod);
        l5.Label = "Correlation TxR";
        plt.ShowLegend();
        plt.SavePng("../../../../../correlation01.png", 3200, 700);
    }

    [Test]
    public async Task AutoCorrelation() {
        var N = 512;
        var sr = 100;
        var plt = new Plot();
        var data1 = DataForFn(i => Filters.Tukey(i - 200, 240, 0.2) + 
                                   0.4 * (1 + 3 * Sin(0.4 * i)) * Filters.Hann(i - 140, 180), N).NormalizeReals();
        var l1 = GraphRealSignals(plt, sr, data1, color: Colors.ForestGreen.WithAlpha(0.8));
        l1.Label = "Time Signal";
        var freqs = fft.FFTToFreq(data1.ToArray());
        var l2 = GraphFrequencies(plt, sr, freqs);
        l2.LineStyle.Pattern = LinePattern.Dashed;
        l2.Label = "Frequencies";
        var corr = fft.Correlate(data1.ToArray(), data1.ToArray());
        var l3 = GraphRealSignals(plt, sr, corr, color: Colors.Black);
        l3.LineStyle.Pattern = LinePattern.Dashed;
        l3.LineStyle.Width = 3;
        l3.Label = "Autocorrelation (direct)";
        freqs.SelectInPlace(x => x.SqrMagnitude());
        fft.FFTFromFreq(freqs);
        var l4 = GraphRealSignals(plt, sr, freqs, color: Colors.BlueViolet);
        l4.Label = "Autocorrelation (PSD)";
        
        plt.ShowLegend();
        plt.SavePng("../../../../../correlation02.png", 3200, 700);
    }

    /// <summary>
    /// Creates graph wavelet_basic which shows how wavelets compare to standard waves:
    /// "Full wave matching" is a constant value, regardless of the amplitude of the original signal at a specific time.
    /// Each Morlet wave is localized in time, but the more localized it is, the more subject it is to interference
    ///  from other waves.
    /// </summary>
    [Test]
    public async Task Wavelet1() {
        var N = 2048;
        var sr = 5120.0;
        var plt = new Plot();
        //Interference will occur at (73-60) = 13Hz intervals
        var data = DataForFnAtRate(x => 2 * BMath.Lerp(0.18, 0.22, x, Cos(Tau * 55 * x), 0)
                +  1 * Cos(Tau * 73 * x)
            , sr, N);
        var l1 = GraphRealSignals(plt, sr, data, color: Colors.Gray);
        l1.Label = "Raw data";
        l1.LineStyle.Pattern = LinePattern.Dotted;
        var l1f = GraphFrequencies(plt, sr, fft.FFTToFreq(data.ToArray()));
        l1f.Label = "Raw data as frequencies";

        var wave = DataForFnAtRate(x => BMath.Cis(0.5, 60, x), sr, N).Normalize();

        //Full wave matching matches against the "average" of the 60Hz wave, so it's a constant value over the range,
        // even though the 60Hz wave fades out.
        var ac = fft.Correlate(data.ToArray(), wave);
        var lac = GraphRealSignals(plt, sr, ac.Select(x => new Complex(x.Magnitude, 0)), color: Maybe<Color>.None);
        lac.Label = $"Full wave matching";

        foreach (var ms in new[] { 50, 100, 200, 360 }) {
                var morlet = DataForFilter(i => Filters.Morlet(i, (int)(sr * ms/1000.0), 60 / sr) * 10, N).Normalize();
                var lmor = GraphRealSignals(plt, sr, morlet.Select(x => x * 100),
                    color: Maybe<Color>.None);
                lmor.LineStyle.Pattern = LinePattern.Dashed;
                lmor.Label = $"{ms}ms Morlet wave (x100)";
                var acm = fft.Correlate(data.ToArray(), morlet);
                var lcm = GraphRealSignals(plt, sr, acm.Select(x => new Complex(x.Magnitude, 0)), color: Maybe<Color>.None);
                lcm.Label = $"{ms}ms Morlet matching";
        }
        /*
        var lr = GraphRealSignals(plt, sr, ac, color: Maybe<Color>.None);
        lr.Label = $"Cos matching";
        lr.LineStyle.Pattern = LinePattern.Dashed;
        var li = GraphRealSignals(plt, sr, ac.Select(x => new Complex(x.Imaginary, 0)), color: Maybe<Color>.None);
        li.Label = $"Sin matching";
        li.LineStyle.Pattern = LinePattern.Dashed;*/

        plt.Axes.Left.Min = -1;
        plt.Axes.Left.Max = 2.4;
        plt.ShowLegend();
        plt.SavePng("../../../../../wavelet_basic.png", 3200, 1000);
    }
    
    [Test]
    public async Task Wavelet2() {
        var N = 16384;
        var sr = 44100.0;
        var plt = new Plot();
        var data = DataForFnAtRate(x => 
            //Note how both the 60 and 80 Hz wavelets have synchronized depressions at a 20Hz rate-
            // this is because a X Hz and Y Hz wave go entirely out of phase every (X-Y) Hz
            BMath.Lerp(0.08, 0.24, x, 1.7 * Cos(Tau * 60 * x), 0) +
            BMath.Lerp(0.2, 0.29, x, 1.2 * Cos(Tau * 80 * x + 1), 0) +
            BMath.Lerp(0.24, 0.32, x, 0, 2 * Cos(Tau * 30 * x + 2))
            , sr, N);
        var l1 = GraphRealSignals(plt, sr, data, yAxis: plt.Axes.Right, color: Colors.Gray);
        l1.Label = "Raw data";
        l1.LineStyle.Pattern = LinePattern.Dotted;
        //var l2 = GraphFrequencies(plt, sr, fft.FFTToFreq(data.ToArray()));
        //l2.Label = "FFT frequencies";

        foreach (var freq in new[] { 20, 30, 40, 50, 60, 70, 80, 90 }) {
            var gauss = DataForFilter(i => Filters.Gaussian(i, 8096, sr/freq*0.1), N).NormalizeReals();
            var ml = DataForFilter(i => Filters.Morlet(i, (int)(sr * 0.1), freq/sr), N).Conjugate();
            ml = fft.ConvolveFilters(gauss, ml);
            var eval = fft.Convolve(data.ToArray(), ml);
            //eval = fft.Convolve(gauss, eval);
            var l = GraphRealSignals(plt, sr, eval.SelectInPlace(x => x.Magnitude), color: Maybe<Color>.None);
            l.Label = $"Morlet {freq}";
            if (freq is 30 or 50 or 80)
                l.LineStyle.Width = 4;
        }

        plt.Axes.Left.Min = 0;
        plt.Axes.Left.Max = 40;
        
        plt.ShowLegend();
        plt.SavePng("../../../../../wavelet_fft.png", 3200, 1000);
    }
    
    /// <summary>
    /// Runs a derivative Gaussian filter over noisy chunked data.
    /// </summary>
    [Test]
    public async Task Gaussian() {
        var N = 512;
        var sr = 5120.0;
        var times = 5;
        var plt = new Plot();
        var gauss = DataForFilter(i => Filters.DGaussian(i, 161, 10), 256);
        var rng = new Random();
        var data = Enumerable.Range(0, times).Select(i => 
                DataForFn(x => 1 - 2 * Cos(Tau * 16 * (x + i * N) / sr) + rng.NextDouble() * 0.4, N))
            .Select((data, ii) => {
                var raw = GraphRealSignals(plt, sr, data, xOffset: ii * N / sr, color: Colors.MediumBlue.WithOpacity(0.6));
                raw.LineStyle.Pattern = LinePattern.Dotted;
                if (ii == 0)
                    raw.Label = "Noisy raw data";
                return data;
            });
        var smear = new ChunkConvolver(new OouraFFT(), N, gauss);
        var si = 0;
        smear.Subscribe(cv => {
            var deriv = GraphRealSignals(plt, sr, cv, xOffset: si * N / sr);
            deriv.LineStyle.Pattern = LinePattern.Dashed;
            var scaled = GraphRealSignals(plt, sr, cv.SelectInPlace(x => x * sr), xOffset: si++ * N / sr, color: Colors.DarkRed);
            scaled.Axes.YAxis = plt.Axes.Right;
            if (si == 1) {
                deriv.Label = "Gaussian derivative (d/di)";
                scaled.Label = "Scaled Gaussian derivative (d/dt)";
            }
        });

        var tderiv = GraphRealSignals(plt, sr, DataForFn(x => Tau * 32 * Sin(Tau * 16 * x / sr), N * times), color: Colors.Fuchsia);
        tderiv.Label = "True derivative (d/dt)";
        tderiv.LineStyle.Pattern = LinePattern.Dashed;
        tderiv.Axes.YAxis = plt.Axes.Right;
        plt.Axes.Right.Label.Text = "d/dt Derivative";
        
        data.ToObservable().Subscribe(smear);
        await smear.Completion.Task;
        plt.Axes.Bottom.Max = times * N / sr;
        plt.Axes.Right.Label.ForeColor = Colors.Red;
        plt.Axes.Left.Label.ForeColor = Colors.Blue;
        plt.Axes.Bottom.Label.ForeColor = Colors.Black;
        plt.Axes.Bottom.Label.Text = "Time (t)";
        plt.Axes.Top.Label.Text = "Samples (i)";
        plt.Axes.Top.Min = 0;
        plt.Axes.Top.Max = plt.Axes.Bottom.Max * sr;
        plt.ShowLegend();
        plt.SavePng("../../../../../gaussian01.png", 3200, 700);
    }
    
    [Test]
    public async Task ChunkedConvolution() {
        var N = 1024;
        var sr = 5120.0;
        var times = 5;
        var plt = new Plot();
        var rng = new Random();
        var fft = new OouraFFT();
        var data = Enumerable.Range(0, times).Select(i =>
                DataForFn(x => 1 - Cos(Tau * 16 * (x + i * N) / sr) + rng.NextDouble() * 0.3, N))
            .Select((data, ii) => {
                GraphRealSignals(plt, sr, data, xOffset: ii * N / sr, 
                    color: Maybe<Color>.None).LineStyle.Pattern = LinePattern.Dashed;
                return data;
            });
        var hhann = DataForFn(i => Filters.HalfHann(i, 121), 512).NormalizeReals();
        var smear = new ChunkConvolver(new OouraFFT(), N, hhann);
        var ii = 0;
        smear.Subscribe(cv => GraphRealSignals(plt, sr, cv, xOffset: ii++ * N / sr));
        data.ToObservable().Subscribe(smear);
        await smear.Completion.Task;
        plt.Axes.Bottom.Max = times * N / sr;
        plt.SavePng("../../../../../chunk01.png", 3200, 700);
    }

    [Test]
    public void ExampleConvolutionPollution() {
        //pollution.png: cyclical pollution of data stream when it is not padded
        var N = 1024;
        var sr = 5120.0;
        var data = DataForFnAtRate(x => 1- Cos(Tau * 8 * x), sr, N);
        var plt = new Plot();
        GraphRealSignals(plt, sr, data.ToArray());
        var hannSmear = DataForFn(i => Filters.HalfHann(i, 73), N).NormalizeReals();
        var fft = new OouraFFT();
        fft.Convolve(data, hannSmear);
        plt.Add.Signal(data.Select(x => x.Real).ToArray(), 1/sr, Colors.Red);
        plt.SavePng("../../../../../pollution.png", 1600, 500);

        //pollution02.png: incorrect response filter produced when response filters are convolved.
        //The black line has polluted data. The blue line has the correct combination.
        N = 32;
        sr = 1000;
        plt = new Plot();
        var smear = FFTHelpers.DataForFilter(i => Filters.HalfHann(i, Math.Min(N-1, (int)(sr*0.12))), N).NormalizeReals();
        GraphRealSignals(plt, sr, smear, color: Colors.DarkRed).Label = "smear";
        var dgaussSize = Math.Min(N-1, (int)(sr * 0.1));
        var dgauss = FFTHelpers.DataForFilter(i => Filters.DGaussian(i, dgaussSize, dgaussSize * 0.1) * sr, N);
        GraphRealSignals(plt, sr, dgauss, color: Colors.Turquoise).Label = "dgauss";
        var smearAndDgaussCorrect = fft.ConvolveFilters(smear.ToArray(), dgauss.ToArray());
        GraphRealSignals(plt, sr, smearAndDgaussCorrect, color: Colors.Blue, xAxis: plt.Axes.Top).Label = "convolve (corrected, double length)";
        var smearAndDgaussNaive = fft.Convolve(smear.ToArray(), dgauss.ToArray());
        GraphRealSignals(plt, sr, smearAndDgaussNaive, color: Colors.Black).Label = "convolve (naive)";
        plt.ShowLegend();
        plt.SavePng("../../../../../pollution02.png", 1600, 500);
    }
    
    [Test]
    public void CreateBasicFilterGraphs() {
        var N = 1024;
        var sr = 5120.0;
        //Since SR/2=2560<2820, the 2820 frequency will appear as 2300
        //Since sr/N=5, all the frequencies except 1283 will have no leakage
        var freqs = new[] { 640, 1283, 1900, 2820 };
        Complex Signal(double x) {
            double total = 0;
            for (int ii = 0; ii < freqs.Length; ++ii)
                total += Cos(Tau * freqs[ii] * x);
            return total;
        }
        var sig = CreateFFTGraph(sr, DataForFnAtRate(Signal, sr, N));
        sig.SavePng("../../../../../freqs1.png", 2400, 700);

        N = 163;
        var ptN = NextPowerOfTwo(N);
        var lowpass = CreateFFTGraph(sr, DataForFilter(x => Filters.LowPass(x, N, 400/sr), ptN));
        lowpass.SavePng("../../../../../lowpass1.png", 1600, 500);
        
        var highpass = CreateFFTGraph(sr, DataForFilter(x => Filters.HighPass(x, N, 600/sr), ptN));
        highpass.SavePng("../../../../../highpass1.png", 1600, 500);

        var bandpass = CreateFFTGraph(sr, new OouraFFT().BandPass(N, ptN, 800/sr, 1400/sr));
        bandpass.SavePng("../../../../../bandpass1.png", 1600, 500);
        
    }

    public static RawSourceWaveStream Make16BitAudio(int sr, int len, IList<Complex> data) {
        var fmt = new WaveFormat(sr, 1);
        var playData = new byte[len * 2];
        for (int ii = 0; ii < len; ++ii) {
            var sample = (short)(Math.Clamp(data[ii].Real, -1, 1) * short.MaxValue);
            playData[2*ii] = (byte)(sample & 0xff);
            playData[2*ii+1] = (byte)((sample >> 8) & 0xff);
        }
        return new RawSourceWaveStream(playData, 0, len * 2, fmt);
    }

    private IEnumerable<Complex[]> ChunkAudio(AudioFileReader file, int N) {
        var channels = file.WaveFormat.Channels;
        var rawData = new float[N * channels];
        var cData = new Complex[N];
        while (file.Read(rawData, 0, N * channels) is {} read and > 0) {
            for (int ii = 0; ii < read / channels; ++ii) {
                float total = 0;
                for (int ic = 0; ic < channels; ++ic)
                    total += rawData[ii * channels + ic];
                cData[ii] = new(total / channels, 0);
            }
            for (int ii = read / channels; ii < N; ++ii)
                cData[ii] = default;
            yield return cData;
        }
    }

    [Test]
    public void Spectrogram() {
        var fn = "../../../../../short-perc-loop.mp3";
        var file = new AudioFileReader(fn);
        var wf = file.WaveFormat;
        var sr = file.WaveFormat.SampleRate;
        var fft = new OouraFFT();
        var mp1 = new Plot();
        var mp2 = new Plot();
        var N = NextPowerOfTwo((int)(file.Length / (wf.BitsPerSample * wf.Channels / 8)));
        var data = ChunkAudio(file, N).First();
        //var lraw = GraphRealSignals(mp1, sr, data, color: Colors.DarkCyan);
        //lraw.Label = "Raw data";
        var autocorr = fft.Correlate(data.ToArray(), data);
        var lac = GraphRealSignals(mp1, sr, autocorr, color: Colors.Coral.WithOpacity(0.6));
        lac.Label = "Autocorrelation";
        var autocorrf = fft.FFTToFreq(autocorr);
        var lacf = GraphFrequencies(mp1, sr, autocorrf.SelectInPlace(x => Math.Log10(Math.Max(0.1, x.Magnitude))));
        lacf.Label = "Power Spectrum";
        mp1.ShowLegend();
        mp1.SavePng("../../../../../spectro01.png", 3200, 700);

    }

    [Test]
    public async Task TestAmplitudeCalculationOnAudio() {
        //ConvSoundTime graph:
        // Blue line: amplitude over time, with half-Hann smoothing.
        // Black line: amplitude derivative, calculated as a sequential convolution on the blue line.
        // Brown line: amplitude derivative, calculated as a single combined convolution of half-Hann + gaussian derivative.
        //
        var fn = "../../../../../short-perc-loop.mp3";
        var file = new AudioFileReader(fn);

        var mp1 = new Plot();
        var mp2 = new Plot();
        var N = 4096;
        double sr = file.WaveFormat.SampleRate;
        var fft = new OouraFFT();
        var allData = new List<Complex>();
        var cdata = ChunkAudio(file, N)
            .Select((cv, ii) => {
                GraphRealSignals(mp1, sr, cv, xOffset: ii * 1.0 * N / sr,
                    color: Maybe<Color>.None);
                return cv;
            }).ToAsyncEnumerable();
        var lowPass = new ChunkConvolver(fft, N, DataForFilter(
            Filters.Identity //DataForFilter(i => Filters.LowPass(i, 2011, 500.0 / sr)
            , N));
        //v1: logic based on smearing the signal stream
        var filterHann = DataForFilter(i => Filters.HalfHann(i, Math.Min(N - 3, (int)(sr * 0.12))), N)
            .NormalizeReals();
        var hannSmear = new ChunkConvolver(fft, N, filterHann.ToArray());
        var dglen = Math.Min(N - 3, (int)(sr * 0.1));
        var filterDGauss =
            DataForFilter(i => Filters.DGaussian(i, dglen, (int)(dglen * 0.1)) * sr, N);
        var dGauss = new ChunkConvolver(fft, N, filterDGauss.ToArray());
        var hannAndDGauss = new ChunkConvolver(fft, N, fft.ConvolveFilters(filterHann, filterDGauss));
        lowPass.Select(cv => {
            allData.AddRange(cv);
            return cv.SelectInPlace(x => x.Magnitude);
        }).Subscribe(hannSmear);
        lowPass.Select(cv => cv.SelectInPlace(x => x.Magnitude)).Subscribe(hannAndDGauss);
        int ih = 0, ic = 0, ihc = 0;
        hannSmear
            .Select(cv => {
                GraphRealSignals(mp2, sr, cv, xOffset: ih++ * 1.0 * N / sr, color: Colors.Navy);
                return cv; //cv.SelectInPlace(x => new(Math.Log10(Math.Max(x.Magnitude, 0.0001)), 0));
            })
            .Subscribe(dGauss);
        dGauss.Subscribe(cv => {
            var deriv = GraphRealSignals(mp2, sr, cv.SelectInPlace(x => x.Real),
                xOffset: ic++ * 1.0 * N / sr, color: Colors.Black, yAxis: mp2.Axes.Right);
            deriv.LineStyle.Pattern = LinePattern.Dotted;
        });
        hannAndDGauss.Subscribe(cv => {
            var deriv = GraphRealSignals(mp2, sr, cv.SelectInPlace(x => x.Real),
                xOffset: ihc++ * N / sr, color: Colors.Maroon, yAxis: mp2.Axes.Right);
            deriv.LineStyle.Pattern = LinePattern.Dashed;
        });
        cdata.ToObservable().Subscribe(lowPass);
        await hannAndDGauss.Completion.Task;
        mp1.SavePng("../../../../../rawSoundTime.png", 3200, 700);
        mp2.Axes.Bottom.Min = 0;
        mp2.Axes.Bottom.Max = 6;
        //mp2.Axes.Bottom.TickGenerator = new NumericFixedInterval() { Interval = N * 1.0 / sr };
        mp2.Axes.Right.Label.Text = "Gaussian derivative";
        mp2.Axes.Right.Max = 10;
        mp2.Axes.Right.Min = -10;
        mp2.SavePng("../../../../../convSoundTimeSignal.png", 3800, 900);
        
        return;
        var outp = new WaveOutEvent();
        outp.Volume = 0.2f;
        outp.Init(Make16BitAudio((int)sr, allData.Count, allData));
        outp.Play();
        do {
            await Task.Delay(1000);
        } while (outp.PlaybackState == PlaybackState.Playing);
    }
    
    
    [Test]
    public async Task TestPowerCalculationOnAudio() {
        //v2: logic based on power calculation
        var fn = "../../../../../short-perc-loop.mp3";
        var file = new AudioFileReader(fn);
        double sr = file.WaveFormat.SampleRate;
        var fft = new OouraFFT();
        var mp3 = new Plot();
        var N = 1024;
        var pN = 64;
        var overlap = new ArrayOverlapperEvent<Complex>(N, 0.25, startWithZeroes:true);
        var psr = sr / (N * overlap.EvictionRate);
        var chunker = new ChunkerEvent<Complex>(pN); //TODO: try push on completion
        var powerData = new List<Complex>();
        var derivData = new List<Complex>();
        var deriv2Data = new List<Complex>();
        overlap.Select(cv => {
            //window
            cv = cv.ToArray();//.SelectInPlace((x, i) => x * Filters.Hann(i - N / 2.0, N));
            //to frequency
            fft.FFTToFreq(cv, true);
            //to power sum
            var power = cv.Sum(x => x.SqrMagnitude()) / (sr * sr);
            //to log power
            return new Complex(PowerBand.LogPower(power), 0);
        }).Subscribe(chunker);
        
        var filterHann = DataForFilter(i => Filters.HalfHann(i, Math.Clamp((int)(psr * 0.08), 3, pN-3)), pN)
            .NormalizeReals();
        var hannSmear = new ChunkConvolver(fft, pN, filterHann.ToArray());
        var filterDGauss = fft.ConvolveFilters(filterHann,
            FFTHelpers.DataForFilter(i => Filters.DGaussian(i, pN - 1, psr * 0.006) * psr, pN));
        var dGauss = new ChunkConvolver(fft, pN, filterDGauss.ToArray());
        //var filterDGauss2 = fft.ConvolveFilters(filterDGauss,
        //    FFTHelpers.DataForFilter(i => Filters.DGaussian(i, pN - 1, psr * 0.003) * psr, pN));
        //var dGauss2 = new ChunkConvolver(fft, pN, filterDGauss2.ToArray());
        chunker.Subscribe(hannSmear);
        chunker.Subscribe(dGauss);
        //chunker.Subscribe(dGauss2);
        //chunker.Subscribe(powerData.AddRange);
        hannSmear.Subscribe(powerData.AddRange);
        dGauss.Subscribe(derivData.AddRange);
        //dGauss2.Subscribe(deriv2Data.AddRange);
        
        foreach (var data in ChunkAudio(file, N).Take(400))
            overlap.OnNext(data);
        overlap.OnCompleted();
        mp3.Add.HorizontalLine(0, color: Colors.DarkGray).Axes.YAxis = mp3.Axes.Right;
        var l1 = GraphRealSignals(mp3, psr, powerData, color: Colors.DarkBlue.WithAlpha(0.3));
        l1.LineStyle.Width = 3;
        l1.Label = "log-power";
        var l2 = GraphRealSignals(mp3, psr, derivData, color: Colors.Maroon.WithAlpha(0.7), yAxis:mp3.Axes.Right);
        l2.LineStyle.Width = 2;
        l2.Label = "derivative";
        //downscaling for graph
        //var l3 = GraphRealSignals(mp3, psr, deriv2Data.Select(x => x/100).ToArray(), color: Colors.Coral, yAxis:mp3.Axes.Right);
        //l3.LineStyle.Width = 2;
        //l3.LineStyle.Pattern = LinePattern.Dashed;
        //l3.Label = "2nd derivative";
        
        mp3.ShowLegend();
        mp3.Axes.Bottom.Max = 6;
        mp3.Axes.Right.Min = -100;
        mp3.Axes.Right.Max = 100;
        mp3.SavePng("../../../../../convSoundTimePower.png", 3800, 900);
        
    }

    
    
    [Test]
    public async Task TestAutocorrelationCalculationOnAudio() {
        var fn = "../../../../../short-perc-loop.mp3";
        var file = new AudioFileReader(fn);
        double sr = file.WaveFormat.SampleRate;
        var fft = new OouraFFT();
        var mp1 = new Plot();
        var N = 262144;
        var data = ChunkAudio(file, N).First();
        GraphRealSignals(mp1, sr, data, color: Colors.FireBrick).Label = "Raw";
        fft.Correlate(data, data.ToArray());
        var l4 = GraphRealSignals(mp1, sr, data, yAxis: mp1.Axes.Right, color: Colors.BlueViolet);
        l4.Label = "Autocorrelation";
        
        mp1.ShowLegend();
        mp1.SavePng("../../../../../convSoundTimeAutocorrelation.png", 3800, 900);
    }

    
    [Test]
    public void TestFFT() {
        var indices = Enumerable.Range(0, 16).Select(x => new Complex(x, 0)).ToArray();
        BitReverseIndices(indices);
        PrintArr(indices, x => x.Real.ToString());
        
        TestFourierForFn(providers[0], ExF1, 1, 4, true);
        TestFourierForFn(providers[1], ExF1, 2, 8, true);
        //TestFourierForFn(providers[2], ExF1, 3, 8, true);
        
        foreach (var fft in providers) {
            TestFourierForFn(fft, ExF1, 2, 8);
            TestFourierForFn(fft, ExF1, 3, 16);
            TestFourierForFn(fft, ExF1, 4, 32);
            TestFourierForFn(fft, ExF2, 2, 8);
            TestFourierForFn(fft, ExF2, 3, 16);
            TestFourierForFn(fft, ExF2, 4, 32);
        }
    }

    [Test]
    public void Timing() {
        void DoTest(bool log) {
            var periodSamples = new[]{
                (2.4, 4), (3.6, 16), (6.1, 64), (11.3, 256), (17.9, 1024), (25.8, 4096), (47.2, 16384)//, (81.0, 65536)
            };
            var rng = new Random();
            var sw = new Stopwatch();
            var rpt = 400;
            var s = periodSamples[^1].Item2;
            FFTHelpers.BitReverseIndices(new Complex[s]);
            
            if(log)
                Console.WriteLine($"\nPerformance for bit reversal:");
            foreach (var (p, N) in periodSamples) {
                sw.Reset();
                for (int ii = 0; ii < rpt; ++ii) {
                    var data = DataForFnOverPeriod(_ => rng.NextDouble() * 10, p, N);
                    sw.Start();
                    FFTHelpers.BitReverseIndices(data);
                    sw.Stop();
                }
                if(log)
                    Console.WriteLine($"{N.ToString(),5}: {(sw.Elapsed.TotalMicroseconds/rpt):000.00,8} us");
            }
            
            foreach (var fft in providers) {
                if(log)
                    Console.WriteLine($"\nPerformance for {fft.ToString()}:");
                foreach (var (p, N) in periodSamples) {
                    sw.Reset();
                    for (int ii = 0; ii < rpt; ++ii) {
                        var data = DataForFnOverPeriod(_ => rng.NextDouble() * 10, p, N);
                        sw.Start();
                        fft.FFTToFreq(data);
                        sw.Stop();
                    }
                    if(log)
                        Console.WriteLine($"{N.ToString(),5}: {(sw.Elapsed.TotalMicroseconds/rpt):000.00,8} us");
                }
            }
        }
        //warmup with no logging
        DoTest(false);
        DoTest(true);
    }
    
    private static Complex ExF1(double t) =>
        5 + 2 * Cos(Tau * t - PI / 2) + 3 * Cos(2 * Tau * t);
    
    private static Complex ExF2(double t) =>
        5 + 4 * Cos(Tau * 1.4 * t - PI / 3) + 2.5 * Cos(1.7 * Tau * t);


    private const double err = 0.00000001;
    private static void AssertEq(Complex a, Complex b) {
        Assert.AreEqual(a.Real, b.Real, err);
        Assert.AreEqual(a.Imaginary, b.Imaginary, err);
    }
    private static void TestFourierForFn(IFFT fft, Func<double, Complex> fn, double period, int samples, bool print=false) {
        var data = DataForFnOverPeriod(fn, period, samples);
        if (print)
            PrintArr(data, x => $"{x.Real:F2}");
        var fdata = data.ToArray();
        fft.FFTToFreq(fdata);
        var frdata = fdata.ToArray();
        if (print)
            PrintArr(frdata);
        for (int ii = 0; ii < samples; ++ii) {
            var x = period * ii / samples;
            //index/period Hz
            var freqSum = Enumerable.Range(0, frdata.Length).Aggregate(Complex.Zero, (acc, fi) =>
                                            acc + frdata[fi] * IExp(2 * Math.PI * fi * x / period)) / samples;
            AssertEq(data[ii], freqSum);
        }

        fft.FFTFromFreq(fdata);
        for (int ii = 0; ii < samples; ++ii) {
            Assert.AreEqual(0, fdata[ii].Imaginary, err);
            AssertEq(fdata[ii], data[ii]);
        }
    }
    private static void PrintArr<T>(T[] arr, Func<T, string>? printer = null) =>
        Console.WriteLine(string.Join("; ", arr.Select(x =>
            printer?.Invoke(x) ?? x.ToString())));


}