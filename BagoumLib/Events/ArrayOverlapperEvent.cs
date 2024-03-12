using System;
using System.Reactive.Subjects;

namespace BagoumLib.Events;

/// <summary>
/// An event that receives arrays of a fixed size, and outputs overlapping arrays of that same size.
/// </summary>
public class ArrayOverlapperEvent<T> : ISubject<T[]> {
    /// <summary>
    /// The number of elements that will be aggregated in each chunk of data.
    /// </summary>
    public int BlockSize { get; }
    
    /// <summary>
    /// The number of indices that will be evicted every time the event yields a new value.
    /// </summary>
    public int EvictionIndices { get; }
    
    /// <summary>
    /// The percentage of the block that will be evicted every time the event yields a new value.
    /// </summary>
    public double EvictionRate { get; }
    private int KeepIndices => BlockSize - EvictionIndices;

    private readonly Event<T[]> ev = new();
    private bool hasFirst = false;
    private readonly T[] prevData;
    private bool startWithZeroes = false;


    public ArrayOverlapperEvent(int blockSize, double evictionRate, bool startWithZeroes = false) {
        if (evictionRate <= 0)
            throw new Exception($"Eviction rate must be greater than zero");
        EvictionRate = evictionRate;
        BlockSize = blockSize;
        EvictionIndices = (int)Math.Round(blockSize * evictionRate);
        if (blockSize % EvictionIndices != 0)
            throw new Exception($"Eviction count {EvictionIndices} must divide block size {BlockSize}");
        prevData = new T[blockSize];
        this.startWithZeroes = startWithZeroes;
    }

    /// <inheritdoc/>
    public void OnNext(T[] value) {
        if (!hasFirst) {
            hasFirst = true;
            if (!startWithZeroes) {
                Array.Copy(value, prevData, BlockSize);
                ev.OnNext(prevData);
                return;
            }
        }
        for (int iv = 0; iv < value.Length; iv += EvictionIndices) {
            Array.Copy(prevData, EvictionIndices, prevData, 0, KeepIndices);
            Array.Copy(value, iv, prevData, KeepIndices, EvictionIndices);
            ev.OnNext(prevData);
        }
    }

    /// <inheritdoc/>
    public void OnCompleted() {
        ev.OnCompleted();
        hasFirst = false;
        Array.Clear(prevData, 0, BlockSize);
    }

    /// <inheritdoc/>
    public void OnError(Exception error) => ev.OnError(error);

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T[]> observer) => ev.Subscribe(observer);
}