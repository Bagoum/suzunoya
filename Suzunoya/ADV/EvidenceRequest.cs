using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Events;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;

namespace Suzunoya.ADV {
/// <summary>
/// An object that allows consumers to request "evidence", which, when provided,
///  interrupts VN execution and runs a <see cref="BoundedContext{T}"/> on top.
/// </summary>
/// <param name="VN">VN process on which this is running.</param>
/// <typeparam name="E">Type of evidence object.</typeparam>
[PublicAPI]
public record EvidenceRequest<E>(IVNState VN) {
    //we push null elements when we interrupt so the lower enclosing context's request
    // can still stick around without being reachable
    private readonly Stack<Token?> requests = new();
    private Token? CurrentRequest => requests.Count > 0 ? requests.Peek() : null;
    /// <summary>
    /// Whether or not there exists a consumer to which evidence can be presented.
    /// </summary>
    public bool CanPresent => CurrentRequest is { };
    
    /// <summary>
    /// Event called when the stack of requests has changed. The stack is not publicly visible, but you
    ///  may examine <see cref="CurrentRequest"/>/<see cref="CanPresent"/> to see the top of the stack.
    /// </summary>
    public Event<Unit> RequestsChanged { get; } = new();

    /// <summary>
    /// Present evidence that has been requested, and run a continuation based on how the request has been configured
    ///  via <see cref="Request"/> or <see cref="WaitForEvidence"/>.
    /// </summary>
    public async Task Present(E evidence) {
        if (CurrentRequest is Token.Interrupt req) {
            var interruption = VN.Interrupt();
            requests.Push(null);
            RequestsChanged.OnNext(default);
            var bcx = req.WhenEvidenceProvided(evidence);
            if (bcx.Identifiable && bcx is not IStrongBoundedContext { LoadSafe: false })
                throw new Exception("Interruption BCTXes should not be identifiable, in order to prevent errant save/load. " +
                                    "Either set the ID to an empty string, or use a StrongBoundedContext with LoadSafe = false.");
            var rct = requests.Count;
            var result = InterruptionStatus.Abort;
            //try/finally in case the interruption BCTX is cancelled or has an exception (in which case we use Abort)
            try {
                result = await bcx;
                if (rct != requests.Count) throw new Exception("Unenclosed requests in interruption");
            } finally {
                requests.Pop();
                RequestsChanged.OnNext(default);
                interruption.ReturnInterrupt(result);
            }
        } else if (CurrentRequest is Token.TCS tcs) {
            requests.Pop();
            RequestsChanged.OnNext(default);
            tcs.OnComplete.SetResult(evidence);
        } else throw new Exception("Cannot provide evidence!");
    }

    /// <summary>
    /// Returns a disposable token that temporarily allows the executing BCTX to be interrupted
    ///  if evidence is provided.
    /// <br/>This cannot be used in a saveable BCTX.
    /// </summary>
    public IDisposable Request(Func<E, BoundedContext<InterruptionStatus>> whenEvidenceProvided) {
        var t = new Token.Interrupt(this, whenEvidenceProvided);
        requests.Push(t);
        RequestsChanged.OnNext(default);
        return t;
    }

    /// <summary>
    /// Return an unskippable task that waits until evidence is provided.
    /// <br/>This is constructed as a BCTX and therefore can be nested within a saveable BCTX.
    /// </summary>
    /// <param name="key">Key used to identify this BCTX.</param>
    public StrongBoundedContext<E> WaitForEvidence(string key) => 
        //waitexternal allows cancellation to work properly
        VN.WrapExternal(key, () => {
            var tcs = new TaskCompletionSource<E>();
            requests.Push(new Token.TCS(tcs));
            RequestsChanged.OnNext(default);
            return tcs.Task;
        });

    private abstract record Token {
        public record TCS(TaskCompletionSource<E> OnComplete) : Token;
        public record Interrupt(EvidenceRequest<E> Stack, Func<E, BoundedContext<InterruptionStatus>> WhenEvidenceProvided) : Token, IDisposable {
            public void Dispose() {
                if (this != Stack.CurrentRequest)
                    throw new Exception("Evidence request disposal called when not on top of request stack");
                Stack.requests.Pop();
                Stack.RequestsChanged.OnNext(default);
            }
        }
    }
}
}