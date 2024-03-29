﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using JetBrains.Annotations;

namespace Suzunoya.ControlFlow {

/// <summary>
/// A class describing a saveable position within a VN. 
/// <br/>A position is a script line ID contextualized by the (non-empty) lists of contexts.
/// <br/>By default, positions are associated with script lines, but
/// can be manually established via vn.OperationID.
/// </summary>
[Serializable]
public class VNLocation {
    /// <summary>
    /// The hierarchically-nested bounded context keys for the current position.
    /// </summary>
    public List<string> Contexts { get; init; }
    /// <summary>
    /// The last operation ID (per <see cref="IVNState.OperationID"/>) executed.
    /// </summary>
    public string LastOperationID { get; init; }

#pragma warning disable 8618
    /// <summary>
    /// Json constructor-- do not use.
    /// </summary>
    public VNLocation() { }
#pragma warning restore 8618
    
    private VNLocation(string lastOperation, IEnumerable<IBoundedContext> ctxs) : 
        this(lastOperation, ctxs.Select(c => c.ID).ToList()) { }
    
    public VNLocation(string lastOperation, List<string> ctxs) {
        this.LastOperationID = lastOperation;
        this.Contexts = ctxs;
    }
    
    /// <summary>
    /// Get the currently executing nested context list, but only if all of them are identifiable.
    /// </summary>
    public static List<string>? GetContexts(IVNState vn) {
        //Don't save the location if there are no contexts (ie. there is no active script)
        List<string>? lines = null;
        foreach (var ctx in vn.Contexts) {
            //Can't save the location if any script in the stack is unidentifiable
            if (!ctx.BCtx.Identifiable)
                return null;
            (lines ??= new()).Add(ctx.ID);
        }
        return lines;
    }

    /// <summary>
    /// Create a <see cref="VNLocation"/> from the current state of the VN if its current location is identifiable.
    /// </summary>
    public static VNLocation? Make(IVNState vn) {
        var ctxs = GetContexts(vn);
        if (ctxs == null)
            return null;
        return new VNLocation(vn.OperationID, ctxs);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is VNLocation b && this == b;
    /// <inheritdoc/>
    public override int GetHashCode() => Contexts.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => 
        string.Join(", ", Contexts) + $"; {LastOperationID}";

    /// <summary>
    /// Return true iff the provided contexts are a nonstrict prefix of this object's contexts.
    /// </summary>
    public bool ContextsMatchPrefix(List<OpenedContext> contexts) {
        if (Contexts.Count < contexts.Count)
            return false;
        for (int ii = 0; ii < contexts.Count; ++ii) {
            if (Contexts[ii] != contexts[ii].BCtx.ID)
                return false;
        }
        return true;
    }
    
    /// <summary>
    /// Whether or not <see cref="Contexts"/> matches the provided contexts list.
    /// </summary>
    public bool ContextsMatch(List<OpenedContext> contexts) {
        if (Contexts.Count != contexts.Count)
            return false;
        for (int ii = 0; ii < Contexts.Count; ++ii) {
            if (Contexts[ii] != contexts[ii].BCtx.ID)
                return false;
        }
        return true;
    }
    
    /// <summary>
    /// Whether or not <see cref="Contexts"/> matches the provided contexts list.
    /// </summary>
    public bool ContextsMatch(List<string> contexts) {
        if (Contexts.Count != contexts.Count)
            return false;
        for (int ii = 0; ii < Contexts.Count; ++ii) {
            if (Contexts[ii] != contexts[ii])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(VNLocation? a, VNLocation? b) {
        return (a is null && b is null) ||
               (!(a is null) && !(b is null) && a.ContextsMatch(b.Contexts) && a.LastOperationID == b.LastOperationID);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(VNLocation? a, VNLocation? b) => !(a == b);
}

/// <summary>
/// A wrapper around a cancellation token that also links to the containing VN.
/// </summary>
public record VNCancellee(IVNState vn, ICancellee cT) : ICancellee {
    /// <inheritdoc/>
    public int CancelLevel => cT.CancelLevel;
    /// <inheritdoc/>
    public ICancellee Root => cT.Root;

    //public VNCancellee BoundCT(ICancellee ncT) => new(vn, new JointCancellee(cT, ncT));
}

}