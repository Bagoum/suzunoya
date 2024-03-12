using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using BagoumLib.Events;
using BagoumLib.Tasks;
using NAudio.Wave;
using NAudio.Extras;

namespace BeatDetectorApp.Models;

public class AudioInstance : IDisposable {
    public AudioFileProxy Audio { get; }
    private WaveFormat Wf => Audio.WaveFormat;
    private int BytesPerSample => Wf.BitsPerSample * Wf.Channels / 8;
    private WaveOutEvent Output { get; } = new();

    public Evented<long> TimeMs { get; private set; } = new(0);
    public Evented<long> SampleIdx { get; private set; } = new(0);
    public long SamplesBufferedTo { get; private set; } = 0;

    public AudioInstance(AudioFileProxy audio) {
        Output.Init(Audio = audio);
        Output.Volume = 0.2f;
        Play();
    }


    public void PlayPause() {
        if (Output.PlaybackState == PlaybackState.Playing) Pause();
        else Play();
    }
    
    private void Play() {
        if (Output.PlaybackState == PlaybackState.Playing) return;
        Output.Play();
        _ = TrackState(DateTimeOffset.Now.ToUnixTimeMilliseconds()).ContinueWithSync();
    }

    private void Pause() {
        Output.Pause();
    }

    public void Stop() {
        Output.Stop();
    }

    private async Task TrackState(long lastSampleUpdate) {
        long previousReceivedSample = 0; 
        SamplesBufferedTo = 0;
        var lastFrameTime = lastSampleUpdate;
        //Since audio playback at a low level is buffered,
        // File.Position is generally *ahead* of the "true" audio position.
        //As such, we store the previous sample position and use the system timer to interpolate
        // towards the current sample position.
        while (Output.PlaybackState == PlaybackState.Playing) {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var nxtSample = Audio.File.Position / BytesPerSample;
            if (nxtSample != SamplesBufferedTo) {
                SampleIdx.Value = previousReceivedSample = SamplesBufferedTo;
                SamplesBufferedTo = nxtSample;
                if (previousReceivedSample > 0) //better first-frame performance
                    lastFrameTime = lastSampleUpdate = now;
            } else {
                SampleIdx.Value = previousReceivedSample +
                                  (long)((lastFrameTime - lastSampleUpdate) * (Wf.SampleRate / 1000.0));
            }
            TimeMs.Value = (long)(SampleIdx / (Wf.SampleRate / 1000.0));
            lastFrameTime += 8;
            while (lastFrameTime > DateTimeOffset.Now.ToUnixTimeMilliseconds()) 
                await Task.Delay(1);
        }
    }

    public void Dispose() {
        Audio.Dispose();
        Output.Dispose();
    }
    
}