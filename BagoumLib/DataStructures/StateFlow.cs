using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures;

/// <summary>
/// A helper to manage state flow between states of type <see cref="T"/>.
/// 
/// </summary>
[PublicAPI]
public abstract class StateFlow<T> {
    /// <summary>
    /// The current state of the flow transfer.
    /// </summary>
    public T State => StateEv.Value;
    
    /// <inheritdoc cref="State"/>
    public Evented<T> StateEv { get; }

    /// <summary>
    /// A canceller that contains the next state for the flow transfer.
    /// </summary>
    public GCancellable<T> NextState { get; protected set; } = new();
    
    /// <summary>
    /// Returns true if <see cref="NextState"/> is cancelled.
    /// </summary>
    public bool Cancelled => NextState.Cancelled(out _);

    /// <inheritdoc cref="StateFlow{T}"/>
    public StateFlow(T s0) {
        StateEv = new(s0);
    }

    /// <summary>
    /// Start the state flow.
    /// </summary>
    public void Start() {
        RunState(Maybe<T>.None);
    }

    /// <summary>
    /// Execute the current <see cref="State"/>.
    /// </summary>
    protected abstract void RunState(Maybe<T> prev);
    
    /// <summary>
    /// Set the next state to execute by calling <see cref="NextState"/>.<see cref="GCancellable{T}.Cancel(T)"/>. The next state will only be executed
    ///  when the current state coroutine calls <see cref="GoToNext"/> or <see cref="GoToNextIfCancelled"/>.
    /// </summary>
    public void SetNext(T next) => NextState.Cancel(next);
    
    /// <summary>
    /// Updates <see cref="State"/>, sets a new token in <see cref="NextState"/>,
    ///  and runs the code for the next state (as implemented virtually in <see cref="RunState"/>).
    /// </summary>
    public void GoToNext(T next) {
        var prev = State;
        NextState = new();
        StateEv.Value = next;
        RunState(prev);
    }

    /// <summary>
    /// If <see cref="NextState"/> is cancelled, then executes that state and returns true.
    /// </summary>
    public bool GoToNextIfCancelled() {
        if (!NextState.Cancelled(out var next))
            return false;
        GoToNext(next);
        return true;
    }

    /// <summary>
    /// Execute the next state as provided in <see cref="NextState"/>, or if <see cref="NextState"/> is not set then use the default instead.
    /// </summary>
    public void GoToNextWithDefault(T deflt) {
        if (NextState.Cancelled(out var next))
            GoToNext(next);
        else
            GoToNext(deflt);
    }

}