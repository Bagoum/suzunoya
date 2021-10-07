using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using JetBrains.Annotations;

namespace Suzunoya.ControlFlow {

/// <summary>
/// A class describing a position.
/// <br/>A position is a script line ID contextualized by the (non-empty) lists of contexts.
/// <br/>By default, positions are associated with script lines, but
/// can be manually established via the vn.RecordPosition(LOCATION) function.
/// </summary>
[Serializable]
public class VNLocation {
    public List<string> Contexts { get; init; }
    public string LastOperationID { get; init; }

    public override string ToString() => 
        string.Join(", ", Contexts) + $"; {LastOperationID}";

    /// <summary>
    /// Json constructor-- do not use.
    /// </summary>
    public VNLocation() { }
    private VNLocation(string lastOperation, IEnumerable<IBoundedContext> ctxs) : 
        this(lastOperation, ctxs.Select(c => c.ID)) { }
    public VNLocation(string lastOperation, IEnumerable<string> ctxs) {
        this.LastOperationID = lastOperation;
        this.Contexts = ctxs.ToList();
    }


    public static VNLocation? Make(IVNState vn) {
        if (vn.CurrentOperationID.Value == null) 
            return null;
        List<IBoundedContext> lines = new();
        foreach (var ctx in vn.Contexts) {
            //Can't save the location if any script in the stack is unidentifiable
            if (string.IsNullOrEmpty(ctx.ID))
                return null;
            lines.Add(ctx);
        }
        return new VNLocation(vn.CurrentOperationID!, lines);
    }

    public override bool Equals(object? obj) => obj is VNLocation b && this == b;
    public override int GetHashCode() => Contexts.GetHashCode();

    public bool ContextsMatch(List<IBoundedContext> contexts) {
        if (Contexts.Count != contexts.Count)
            return false;
        for (int ii = 0; ii < Contexts.Count; ++ii) {
            if (Contexts[ii] != contexts[ii].ID)
                return false;
        }
        return true;
    }
    public bool ContextsMatch(List<string> contexts) {
        if (Contexts.Count != contexts.Count)
            return false;
        for (int ii = 0; ii < Contexts.Count; ++ii) {
            if (Contexts[ii] != contexts[ii])
                return false;
        }
        return true;
    }

    public static bool operator ==(VNLocation a, VNLocation b) {
        return a.ContextsMatch(b.Contexts) && a.LastOperationID == b.LastOperationID;
    }

    public static bool operator !=(VNLocation a, VNLocation b) => !(a == b);
}

public record VNOpTracker(VNLocation? location, ICancellee cT) : ICancellee {
    public int CancelLevel => cT.CancelLevel;
    public bool Cancelled => cT.Cancelled;
    public ICancellee Root => cT.Root;
}

}