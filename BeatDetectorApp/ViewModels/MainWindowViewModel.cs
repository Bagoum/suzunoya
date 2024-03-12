using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia.Media;
using BagoumLib;
using BagoumLib.Mathematics;
using BagoumLib.SignalProcessing;
using NAudio.Wave;
using ReactiveUI;
using BagoumLib.Tasks;
using BeatDetectorApp.Models;
using ScottPlot;
using ScottPlot.AutoScalers;
using ScottPlot.Avalonia;
using ScottPlot.Colormaps;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using static System.Math;
using Color = ScottPlot.Color;
using Colors = ScottPlot.Colors;
using FastFourierTransform = NAudio.Dsp.FastFourierTransform;

namespace BeatDetectorApp.ViewModels;

public class MainWindowViewModel : ViewModelBase {
    
    private long time;
    public long Time {
        get => time;
        private set => this.RaiseAndSetIfChanged(ref time, value);
    }
    public AudioInstance? CurrentAudio { get; private set; }
    public BehaviorSubject<Func<AvaPlot, AvaPlot, Task>> Plotter { get; }
    
    public MainWindowViewModel() {
        Logging.Logs.RegisterListener(new TrivialLogListener(lm => {
            Console.WriteLine(lm.Message);
            if (lm.Exception != null) Console.WriteLine(lm.Exception.Message);
        }));
        Plotter = new(async (spec, hist) => {});
    }
    
    public void PlayNew() {
        CurrentAudio?.Stop();
        CurrentAudio?.Dispose();
        var file = new AudioFileReader("../../../../../1-02 極彩色.mp3");
        beats = new(file.WaveFormat.SampleRate);
        lastDrawnHistIndex = 0;
        lastDrawnPowerIndex = 0;
        nextHeatmapGraphIndex = 0;
        powerFreqs = new double[beats.NPower];
        powerVals = new double[beats.NPower];
        Plotter.OnNext(async (_spec, _hist) => {
            var spec = _spec.Plot;
            var hist = _hist.Plot;
            spec.Clear();
            spec.Axes.AutoScaler = new ScNoAutoScaler();
            var ry = spec.Axes.Right;
            if (RenderBands) {
                var bars = new Bar[beats.NSignalBands];
                //Console.WriteLine($"{sampleIdx},{beats.Bands[0].TotalSamples},{audio.SamplesBufferedTo}");
                for (var ei = 0; ei < bars.Length; ++ei) {
                    var band = beats.SigBands[ei];
                    bars[ei] = new Bar() {
                        Position = (HzToX(band.LowLimit) + HzToX(band.HighLimit)) / 2,
                        Value = 0,
                        ValueBase = 0,
                        Size = (HzToX(band.LowLimit) - HzToX(band.HighLimit)),
                        BorderColor = new Color(0, 0, 0, 0),
                        FillColor = ColorForBand(ei, beats.NSignalBands)
                    };
                }
                spec.Add.Bars(bars).Axes.YAxis = ry;
                ry.Min = 0;
                ry.Max = 0.2;
            }
            if (RenderHeat) {
                for (int ii = 0; ii < heatmapHistory; ++ii) {
                    var data = new double[1, beats.NPowerBands + 2];
                    var hm = new ScHeatmap(data) { CellAlignment = Alignment.LowerLeft };
                    spec.PlottableList.Add(hm);
                    heatMap[ii] = new(hm, data);
                    hm.Colormap = new NonNormalizedColormap(new Viridis());
                }
            }
            for (var ii = 0; ii < beats.NPower; ++ii)
                powerFreqs[ii] = HzToX(Math.Max(0.1, ii * beats.HzPerPowerBucket));
            if (RenderLine)
                spec.Add.SignalXY(powerFreqs, powerVals, Colors.LightBlue);
            LogMinorTickGenerator minorTickGen = new() { Divisions = 10 };
            spec.Axes.Bottom.TickGenerator = new NumericAutomatic() {
                MinorTickGenerator = minorTickGen,
                LabelFormatter = f => f switch {
                    0 => "",
                    1 => "ALL",
                    _ => $"{XToHz(f):N0}"
                }
            };
            spec.GetDefaultGrid().MinorLineStyle.Width = 1;
            spec.GetDefaultGrid().MinorLineStyle.Color = Colors.Black.WithOpacity(0.05);
            spec.Axes.SetLimits(1, HzToX(beats.SigBands[^1].HighLimit), minDb, maxDb);
            
            /*var strm = spec.Add.DataStreamer((int)(beats.SampleRate *  6/strmDecimate), strmDecimate);
            strm.ViewWipeRight();
            strm.Color = ColorForBand(4);*/
            hist.Clear();
            hist.Axes.AutoScaler = new ScNoAutoScaler();
            hist.Axes.Bottom.TickLabelStyle.Rotation = -90;
            var vline = hist.Add.VerticalLine(0, 2f, Colors.Black);
            void AddBands(IReadOnlyList<IBand> bands, int streamCache, double streamDelta) {
                var nBands = bands.Count;
                for (int ei = 0; ei < nBands; ++ei) {
                    var energy = hist.Add.DataStreamer(streamCache, streamDelta);
                    energy.ManageAxisLimits = false;
                    energy.ViewWipeRight();
                    energy.Color = ColorForBand(ei, nBands);
                }
                hist.Axes.SetLimits(0, histShowTime, -1.5, nBands - 0.3);
                hist.Axes.Left.TickGenerator = new NumericAutomatic() {
                    IntegerTicksOnly = true,
                    LabelFormatter = f => {
                        var x = (int)f;
                        if (x == -1) return "ALL";
                        if (x is 0) return "";
                        if (x < 0 || x >= bands.Count) return "";
                        return $"\u2192{bands[x].HighLimit:N0}";
                    }
                };
            }
            if (RenderHistForPower) {
                AddBands(beats.PowerBands, (int)(beats.PowerSampleRate * histShowTime), 1 / beats.PowerSampleRate);
            } else {
                AddBands(beats.SigBands, (int)(beats.SampleRate * histShowTime / strmDecimate), strmDecimate / beats.SampleRate);
            }
        });
        var inst = new AudioInstance(new(file));
        inst.TimeMs.Subscribe(x => {
            Time = x - lastReceivedTime;
            lastReceivedTime = x;
        });
        inst.SampleIdx.Subscribe(idx => _ = Replot(inst, idx).ContinueWithSync());
        CurrentAudio = inst;
    }

    private long lastReceivedTime = 0;
    private long lastDrawnHistIndex = 0;
    private int lastDrawnPowerIndex = 0;

    private BeatDetector beats = null!;
    private bool plotting = false;
    private Random rng = new();
    private int strmDecimate = 4;
    private int histShowTime = 3;
    private int barAvgBy = 4096;
    private double[] powerFreqs;
    private double[] powerVals;
    private static readonly int heatmapHistory = 800;
    private HeatMapRecord[] heatMap = new HeatMapRecord[heatmapHistory];
    private int nextHeatmapGraphIndex = 0;
    private bool RenderLine = false;
    private bool RenderBands = false;
    private bool RenderHeat = true;
    private bool RenderHistForPower = true;
    private float minDb = 0;
    private float maxDb = 55;

    private record HeatMapRecord(ScHeatmap Hm, double[,] Data) {
    }

    private Color ColorForBand(int ei, int total) {
        if (ei == 0)
            return Colors.DarkOliveGreen.WithAlpha(0.7);
        var ratio = (float) Math.Sqrt(ei / (total * 0.9));
        return new Color(BMath.Lerp(0.1f, 0.8f, ratio), 0.1f, BMath.Lerp(0.8f, 0.1f, ratio), 0.7f);
    }

    private double XToHz(double x) => BeatDetector.MinHz * Math.Pow(2, x/3.0 - 1);
    private double HzToX(double hz) => (Math.Log2(Math.Max(0.001, hz / BeatDetector.MinHz)) + 1) * 3;

    private CoordinateRect HeatmapExtents(int deltaUpdate) {
        return new(
            HzToX(beats.PowerBands[0].LowLimit), HzToX(beats.PowerBands[^1].HighLimit), 
            BMath.Lerp(minDb, maxDb, deltaUpdate * 1.0 / heatmapHistory),
            BMath.Lerp(minDb, maxDb, (deltaUpdate + 1) * 1.0 / heatmapHistory));
    }
    
    private async Task Replot(AudioInstance audio, long sampleIdx) {
        bool didUpdatePower = false;
        lock (audio.Audio.SampleBuffer) {
            (_, didUpdatePower) = beats.Update(audio.Audio.SampleBuffer, (int)sampleIdx, powerVals);
        }
        double LogPowerToDb(double pow) => 10 * pow;
        double PowerToDb(double pow) => LogPowerToDb(PowerBand.LogPower(pow));
        if (didUpdatePower)
            powerVals.SelectInPlace(PowerToDb);
        
        var drawHistIndex = RenderHistForPower ?
            Math.Min(beats.PowerBands[0].DerivativeHistory.TotalAdds, 
                (int)(sampleIdx * beats.PowerSampleRate/beats.SampleRate - beats.PowerBands[0].PowerN)) : 
            Math.Min(beats.SigBands[0].History.TotalAdds, (int)sampleIdx - 2 * beats.NSignal);
        if (drawHistIndex <= lastDrawnHistIndex) //these indices are exclusive
            return;
        
        if (RenderHeat) {
            while (lastDrawnPowerIndex < beats.PowerBands[0].LogPowerHistory.TotalAdds) {
                var hmr = heatMap[nextHeatmapGraphIndex];
                for (int ih = 0; ih < hmr.Data.GetLength(1); ++ih) {
                    //ih=0 (ib=0) is total. ih=2 should copy ih=1 (ib=1). ih=4 should copy ih=3 (ib=2).
                    var ib = ih switch {
                        0 => 0,
                        1 => ih,
                        2 or 3 => ih - 1,
                        _ => ih - 2
                    };
                    hmr.Data[0, ih] = BMath.RatioC(minDb, maxDb, 
                        (float)LogPowerToDb(beats.PowerBands[ib].LogPowerHistory.TrueIndex(lastDrawnPowerIndex)));
                }
                hmr.Hm.MarkDirty();
                ++lastDrawnPowerIndex;
                nextHeatmapGraphIndex = (nextHeatmapGraphIndex + 1) % heatMap.Length;
            }
        }

        Plotter.OnNext(async (_spec, _hist) => {
            if (plotting) return;
            plotting = true;
            //Console.WriteLine($"{lastDrawnSampleIndex}->{drawSampleIndex}");
            var spec = _spec.Plot;
            var hist = _hist.Plot;
            double VisualOnlyScalingFactor(int ei) => Math.Pow(24, ei * 1.0 / beats.NSignalBands);
            if (spec.GetPlottables<BarPlot>().FirstOrDefault()?.Bars is Bar[] bars) {
                for (var ei = 0; ei < beats.NSignalBands; ++ei) {
                    var band = beats.SigBands[ei];
                    double avg = 0;
                    for (int ii = drawHistIndex - barAvgBy; ii < drawHistIndex; ++ii)
                        avg += band.History.TrueIndex(ii);
                    bars[ei].Value = avg / barAvgBy;
                }
            }

            while (spec.RenderManager.IsRendering)
                await Task.Delay(TimeSpan.FromMicroseconds(50));
            if (RenderHeat) {
                for (int updateDelta = 0; updateDelta < heatMap.Length; ++updateDelta) {
                    var hmr = heatMap[BMath.Mod(heatMap.Length, nextHeatmapGraphIndex - 1 - updateDelta)];
                    hmr.Hm.Extent = HeatmapExtents(updateDelta);
                }
            }
            _spec.Refresh();
            /*
            for (int ih = 0; ih < beats.NHistory; ++ih) {
                for (int ib = 0; ib < beats.NBands; ++ib) {
                    if (beats.Bands[ih][ib].IsSignalBeat > 0.5)
                        peaks.Add(new(ih, 2 * ib + Math.Min(1, 
                            Math.Clamp(beats.Bands[ih][ib].DSigmaRoll / 100, -0.8, 0.8))));
                }
            }*/

            var strms = hist.PlottableList;
            var vl = (VerticalLine)strms[0];
            var ds0 = (DataStreamer)strms[1];
            List<double>[] ndata;
            if (RenderHistForPower) {
                ndata = new List<double>[beats.NPowerBands];
                for (int ib = 0; ib < beats.NPowerBands; ++ib) {
                    var b = beats.PowerBands[ib];
                    ndata[ib] = new();
                    for (var ii = lastDrawnHistIndex; ii < drawHistIndex; ++ii) {
                        double avg = 0;
                        int ai = 0;
                        for (; ai < strmDecimate && ii + ai < drawHistIndex; ++ai)
                            //avg += b.History.TrueIndex((int)ii + ai);
                            avg += b.DerivativeHistory.TrueIndex((int)ii + ai);
                        //var v = b.LogPowerHistory.TrueIndex((int)ii);
                        //ndata[ib].Add(ib + offset + BMath.RatioC(minDb, maxDb, (float)LogPowerToDb(v)));
                        var offset = ib switch {
                            0 => -1,
                            1 => -0.5,
                            2 => -0.25,
                            _ => 0
                        };
                        ndata[ib].Add(ib + offset + BMath.RatioC(0, 70, (float)avg/ai));
                    }
                }
            } else {
                ndata = new List<double>[beats.NSignalBands];
                for (int ib = 0; ib < beats.NSignalBands; ++ib) {
                    var b = beats.SigBands[ib];
                    ndata[ib] = new();
                    for (var ii = lastDrawnHistIndex; ii < drawHistIndex; ii += strmDecimate) {
                        double avg = 0;
                        int ai = 0;
                        for (; ai < strmDecimate && ii + ai < drawHistIndex; ++ai)
                            //avg += b.History.TrueIndex((int)ii + ai);
                            avg += b.DerivativeHistory.TrueIndex((int)ii + ai);
                        var v = avg / ai;
                        var offset = ib switch {
                            0 => -1,
                            1 => -0.5,
                            2 => -0.25,
                            _ => 0
                        };
                        //ndata[ib].Add(ib + offset + BMath.RatioC(0, 0.2f, (float)v));
                        //ndata[ib].Add(ib + offset + BMath.RatioC(-5, 0, (float)Math.Log10(Math.Max(0.000001, v))));
                        ndata[ib].Add(ib + offset + Math.Clamp(v * 0.17, -0.7, 0.7));
                    }
                }
            }
            for (int ib = 0; ib < strms.Count - 1; ++ib) {
                var strm = (DataStreamer)strms[ib + 1];
                strm!.AddRange(ndata[ib]);
            }
            vl.X = (ds0.Data.NextIndex % ds0.Data.Length) * ds0.Period;
            
            //hist.Add.Scatter(peaks, Colors.Black).LineStyle.IsVisible = false;
            _hist.Refresh();
            lastDrawnHistIndex = drawHistIndex;
            plotting = false;
        });
    }
    

    public void PlayPause() => CurrentAudio?.PlayPause();
}