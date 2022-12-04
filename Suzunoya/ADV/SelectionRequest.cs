using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib.Events;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;

namespace Suzunoya.ADV {

/// <summary>
/// An object that allows consumers to request that the player select one of several options.
/// </summary>
/// <param name="VN">VN process on which this is running.</param>
/// <typeparam name="C">Type of option. In the simplest case, this may be a string.</typeparam>
[PublicAPI]
public record SelectionRequest<C>(IVNState VN) {
    //Unlike EvidenceRequest, we do not allow stacking, since selection is always a blocking request
    private Request? currentRequest;

    /// <summary>
    /// Whether or not there exists a consumer to which a selection can be provided.
    /// </summary>
    public bool CanSelect => currentRequest is { };
    
    /// <summary>
    /// Event called when there is a new selection request, in which case it contains the selection options,
    ///  or when a selection request is complete, in which case it contains null.
    /// </summary>
    public Event<C[]?> RequestChanged { get; } = new();


    /// <summary>
    /// Make a selection that has been requested via <see cref="WaitForSelection"/>.
    /// <br/>Note that this requires equality operators to be well-defined.
    /// </summary>
    public void MakeSelection(C value) {
        if (currentRequest == null)
            throw new Exception("Cannot make selection when there is no selection request");
        var eq = EqualityComparer<C>.Default;
        for (int ii = 0; ii < currentRequest.Options.Length; ++ii)
            if (eq.Equals(value, currentRequest.Options[ii])) {
                MakeSelection(ii);
                return;
            }
        throw new Exception($"Value {value} is not one of the selectable values");
    }
    
    /// <summary>
    /// Make a selection that has been requested via <see cref="WaitForSelection"/>.
    /// </summary>
    public void MakeSelection(int index) {
        if (currentRequest is not { } req)
            throw new Exception("Cannot make selection when there is no selection request");
        currentRequest = null;
        RequestChanged.OnNext(null);
        req.OnComplete.SetResult((index, req.Options[index]));
    }
    
    /// <summary>
    /// Return an unskippable task that waits until a selection is made.
    /// <br/>This is constructed as a BCTX and therefore can be nested within a saveable BCTX.
    /// </summary>
    /// <param name="key">Key used to identify this BCTX.</param>
    /// <param name="options">Non-empty array of options. Do not modify the array after passing it to this function.</param>
    public StrongBoundedContext<(int index, C value)> WaitForSelection(string key, params C[] options) =>
        VN.WrapExternal(key, () => {
            if (currentRequest != null)
                throw new Exception("Cannot request a selection when one already exists");
            if (options.Length == 0)
                throw new Exception("No options provided");
            currentRequest = new(options, new TaskCompletionSource<(int, C)>());
            RequestChanged.OnNext(options);
            return currentRequest.OnComplete.Task;
        });

    private record Request(C[] Options, TaskCompletionSource<(int index, C value)> OnComplete);
}
}