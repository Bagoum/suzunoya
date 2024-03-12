using System;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Events;

namespace BagoumLib.SignalProcessing;

/// <summary>
/// Convolve two sequences in the time domain together, where the first sequence is a series of chunks.
/// <br/>Extra information at the start and end of the resulting convolution is discarded.
/// <br/>Note: the Subscribe repeatedly yields the same underlying array. The yielded array must be processed
///  before calling OnNext.
/// </summary>
public class ChunkConvolver : IObserver<Complex[]>, IObservable<Complex[]> {
    /// <summary>
    /// Task that can be used in place of subscribing to <see cref="OnCompleted"/>.
    /// </summary>
    public TaskCompletionSource<bool> Completion { get; } = new();
    private readonly IFFT fft;
    private readonly int srcChunkLen;
    private readonly int npReqChunkLen;
    private readonly int startPad;
    private readonly int endPad;
    private readonly Complex[] fData;
    private Complex[] prevData;
    private Complex[] nextData;
    private readonly Complex[] fResp;
    private bool isFirst = true;
    private readonly Event<Complex[]> processedChunk = new();


    /// <summary>
    /// </summary>
    /// <param name="fft">FFT provider.</param>
    /// <param name="srcChunkLen">The length of each block provided by <see cref="OnNext"/>.</param>
    /// <param name="resp">Response filter. The length must be a power of two.</param>
    /// <param name="useCorrelation">If true, computes a correlation instead of a convolution.
    /// (Note that a convolution is symmetric, but a correlation is not. This computes src*resp^.)</param>
    /// <returns>A series of chunks of convolved data, each of length <see cref="srcChunkLen"/>.</returns>
    public ChunkConvolver(IFFT fft, int srcChunkLen, Complex[] resp, bool useCorrelation=false) {
        if (!FFTHelpers.IsPowerOfTwo(resp.Length))
            throw new Exception("Response function length must be a power of two");
        this.fft = fft;
        this.srcChunkLen = srcChunkLen;
        (endPad, startPad) = resp.GetFilterLength();
        if (startPad > srcChunkLen || endPad > srcChunkLen)
            throw new Exception($"Chunk length ({srcChunkLen}) cannot be less than either padding ({startPad} start, {endPad} end)");
        npReqChunkLen = Math.Max(FFTHelpers.NextPowerOfTwo(srcChunkLen + startPad + endPad), resp.Length);
        fResp = resp.PadFilterToLength(npReqChunkLen);
        fft.FFTToFreq(fResp);
        if (useCorrelation) {
            fResp.Conjugate();
            //For a convolution, the forward length smears the last results of the source stream outwards,
            // requiring end padding.
            //For a correlation, the forward length matches the first results of the source stream at even smaller
            // indices, requiring start padding.
            (endPad, startPad) = (startPad, endPad);
        }
        fData = new Complex[npReqChunkLen];
        prevData = new Complex[srcChunkLen];
        nextData = new Complex[srcChunkLen];
    }
    private void ConvolveChunk(Complex[] chunk) {
        Array.Clear(fData, 0, npReqChunkLen);
        for (int ii = 0; ii < chunk.Length; ++ii)
            fData[ii + startPad] = chunk[ii];
        fft.ConvolveOnFreq(fData, fResp);
    }
    private void SumStartPadData() {
        for (int ii = 0; ii < startPad; ++ii)
            prevData[^(startPad - ii)] += fData[ii];
    }
    private void SumCurrentAndEndPadData() {
        for (int ii = 0; ii < srcChunkLen; ++ii)
            prevData[ii] += fData[ii + startPad];
        for (int ii = 0; ii < endPad; ++ii)
            nextData[ii] += fData[ii + srcChunkLen + startPad];
    }

    /// <inheritdoc/>
    public void OnNext(Complex[] source) {
        if (source.Length != srcChunkLen)
            throw new Exception($"Chunk length must be {srcChunkLen}");
        ConvolveChunk(source);
        if (isFirst) {
            isFirst = false;
            //discard start pad data for initial block
            SumCurrentAndEndPadData();
            return;
        }
        SumStartPadData();
        processedChunk.OnNext(prevData);
        Array.Clear(prevData, 0, srcChunkLen);
        (prevData, nextData) = (nextData, prevData);
        SumCurrentAndEndPadData();
    }

    /// <inheritdoc/>
    public void OnCompleted() {
        processedChunk.OnNext(prevData);
        processedChunk.OnCompleted();
        Completion.SetResult(true);
    }

    /// <inheritdoc/>
    public void OnError(Exception error) => processedChunk.OnError(error);

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<Complex[]> observer) => processedChunk.Subscribe(observer);
}