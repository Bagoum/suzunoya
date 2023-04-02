using System;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Events;
using Suzunoya.ControlFlow;

namespace Suzunoya.ADV {
/// <summary>
/// An <see cref="EvidenceRequest{E}"/> that supports top-level evidence presentation in an ADV.
/// </summary>
public record ADVEvidenceRequest<E>(ADVManager ADV, IVNState VN) : EvidenceRequest<E>(VN, ADV) {
    /// <summary>
    /// Request handling for evidence presentation at the top level.
    /// </summary>
    private OverrideEvented<TopLevelHandler?> TopLevelRequest { get; } = new(null);

    /// <summary>
    /// True iff there exists a top-level consumer for evidence and the ADV can currently accept a top-level
    ///  context execution.
    /// </summary>
    public bool CanPresentTopLevel => !ADV!.VNIsExecuting && TopLevelRequest.Value != null;
    
    /// <summary>
    /// True iff there exists a top-level consumer for evidence and the ADV can currently accept a top-level
    ///  context execution, or there is an interruptable context currently executing.
    /// </summary>
    public bool CanPresentAny => CanPresent || CanPresentTopLevel;
    
    /// <summary>
    /// Present evidence that has been requested, and run a top-level <see cref="BoundedContext{T}"/> on the ADV.
    /// <br/>Make sure <see cref="CanPresentTopLevel"/> is true before calling this.
    /// </summary>
    public Task PresentTopLevel(E evidence) {
        if (!CanPresentTopLevel)
            throw new Exception($"Presented top-level evidence when {nameof(CanPresentTopLevel)} is false");
        return TopLevelRequest.Value!.Execute(this, evidence);
    }

    /// <summary>
    /// Present evidence that has been requested, either in an interruptable context or at the top-level ADV.
    /// <br/>Make sure <see cref="CanPresentAny"/> is true before calling this.
    /// </summary>
    public Task PresentAnyLevel(E evidence) {
        if (CanPresent)
            return Present(evidence);
        else
            return PresentTopLevel(evidence);
    }

    /// <summary>
    /// Provide a handler that handles evidence presented at the top level.
    /// </summary>
    public IDisposable RequestTopLevel<T>(Func<E, BoundedContext<T>>? handler) =>
        TopLevelRequest.AddConst(handler == null ? null : new TopLevelHandler.For<T>(handler));
    
    
    private abstract record TopLevelHandler {
        public abstract Task Execute(ADVEvidenceRequest<E> req, E evidence);

        public record For<T>(Func<E, BoundedContext<T>> Handler) : TopLevelHandler {
            public override Task Execute(ADVEvidenceRequest<E> req, E evidence) =>
                req.ADV!.ExecuteVN(req.AssertInterruptionSafe(Handler(evidence)));
        }
    }

}
}