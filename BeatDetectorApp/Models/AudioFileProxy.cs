using System;
using BagoumLib.DataStructures;
using NAudio.Wave;

namespace BeatDetectorApp.Models;

public record AudioFileProxy(AudioFileReader File) : ISampleProvider, IDisposable {
    public WaveFormat WaveFormat => File.WaveFormat;
    public CircularList<float> SampleBuffer { get; } = new(65536);
    
    public int Read(float[] buffer, int offset, int count) {
        var read = File.Read(buffer, offset, count);
        lock (SampleBuffer) {
            for (int ii = 0; ii < read; ii += WaveFormat.Channels) {
                var total = 0f;
                for (int ic = 0; ic < WaveFormat.Channels && ii + ic < read; ++ic)
                    total += buffer[ii + ic];
                SampleBuffer.Add(total / WaveFormat.Channels);
            }
        }
        return read;
    }

    public void Dispose() => File.Dispose();
}