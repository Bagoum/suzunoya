using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive;
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
public record OptionSelector<C>(IVNState VN) {
    //Unlike EvidenceRequest, we do not allow stacking, since selection is always a blocking request
    /// <summary>
    /// Whether or not there exists a consumer to which a selection can be provided.
    /// </summary>
    public bool CanSelect => CurrentRequest.Value is not null;
    
    /// <summary>
    /// Event called when there is a new selection request, in which case it contains the selection options,
    ///  or when a selection request is complete, in which case it contains null.
    /// </summary>
    public Evented<Request?> CurrentRequest { get; } = new(null);
    
    /// <summary>
    /// Return an unskippable task that waits until a selection is made.
    /// <br/>This is constructed as a BCTX and therefore can be nested within a saveable BCTX.
    /// </summary>
    /// <param name="key">Key used to identify this BCTX.</param>
    /// <param name="options">Non-empty array of options. Do not modify the array after passing it to this function.</param>
    public StrongBoundedContext<(int index, C value)> WaitForSelection(string key, params C[] options) =>
        VN.WrapExternal(key, async () => {
            if (CanSelect)
                throw new Exception("Cannot request a selection when one already exists");
            if (options.Length == 0)
                throw new Exception("No options provided");
            var nxt = new Request(options, new TaskCompletionSource<(int, C)>());
            CurrentRequest.OnNext(nxt);
            try {
                return await nxt.OnComplete.Task;
            } finally {
                CurrentRequest.OnNext(null);
            }
        });

    /// <summary>
    /// A set of options that a user may select from.
    /// </summary>
    public record Request(C[] Options, TaskCompletionSource<(int index, C value)> OnComplete) {
        /// <summary>
        /// Make a selection from the provided options.
        /// </summary>
        public void Select(int index) => OnComplete.SetResult((index, Options[index]));

        /// <inheritdoc cref="Select(int)"/>
        public void SelectValue(C value) {
            var eq = EqualityComparer<C>.Default;
            for (int ii = 0; ii < Options.Length; ++ii)
                if (eq.Equals(value, Options[ii])) {
                    Select(ii);
                    return;
                }
            throw new Exception($"Value {value} is not one of the selectable values");
        }
    }
}
}