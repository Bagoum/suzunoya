using System;
using System.Reactive.Subjects;

namespace BagoumLib.Events;

/// <summary>
/// An event that receives items and outputs them in non-overlapping arrays of length <see cref="BlockSize"/>.
/// <br/>Note that the final partial chunk will not be output when <see cref="OnCompleted"/> is called, unless
/// <see cref="pushOnCompletion"/> is set to true, in which case it will be output with the rest of the array
///  set to `default`.
/// </summary>
public class ChunkerEvent<T> : IObserver<T>, IObservable<T[]> {
    /// <summary>
    /// The in-process chunk of data that will be issued to observers
    ///  when <see cref="AllotedIndicies"/> equals <see cref="BlockSize"/>.
    /// </summary>
    public T[] NextChunk { get; }
    
    /// <summary>
    /// The number of elements that will be aggregated in each chunk of data.
    /// </summary>
    public int BlockSize { get; }
    
    /// <summary>
    /// The number of indices that have been set in <see cref="NextChunk"/>.
    /// </summary>
    public int AllotedIndicies { get; private set; }

    private readonly Event<T[]> ev = new();
    private readonly bool pushOnCompletion = false;

    public ChunkerEvent(int blockSize, bool pushOnCompletion=false) {
        BlockSize = blockSize;
        NextChunk = new T[blockSize];
        this.pushOnCompletion = pushOnCompletion;
    }

    /// <inheritdoc/>
    public void OnNext(T value) {
        NextChunk[AllotedIndicies++] = value;
        if (AllotedIndicies == BlockSize) {
            ev.OnNext(NextChunk);
            Array.Clear(NextChunk, 0, BlockSize);
            AllotedIndicies = 0;
        }
    }

    /// <inheritdoc/>
    public void OnCompleted() {
        if (AllotedIndicies > 0 && pushOnCompletion)
            ev.OnNext(NextChunk);
        ev.OnCompleted();
        Array.Clear(NextChunk, 0, BlockSize);
        AllotedIndicies = 0;
    }

    /// <inheritdoc/>
    public void OnError(Exception error) => ev.OnError(error);

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T[]> observer) => ev.Subscribe(observer);
}