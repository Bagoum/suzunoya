using System;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using static BagoumLib.Mathematics.GenericOps;

namespace BagoumLib.Transitions {
public record StatusApplier<T> : TransitionBase<T> {
    //----- Required variables
    
    /// <summary>
    /// Value to apply given time T.
    /// If <see cref="InitialValue"/> is defined, this will be added to it.
    /// </summary>
    public Func<float, T> Valuer { get; init; }
    
    /// <summary>
    /// Method to update value.
    /// </summary>
    public Action<T> Apply { get; init; }
    
    //----- Required variables with defaults provided

    //-----Optional variables set via initializer syntax (or possibly fluent API)

    /// <summary>
    /// If present, will be used as an offset for Valuer.
    /// Evaluated when the transition is run.
    /// </summary>
    public Func<T>? InitialValue = null;
    
    
    private static readonly Func<T, T, T> Add = GetAddOp<T>().add;
    

    public StatusApplier(Func<float, T> value, float time, Action<T> apply, ICancellee? cT = null) {
        Valuer = value;
        Time = time;
        Apply = apply;
        CToken = cT;
    }

    protected override T ApplyStart() {
        var offset = InitialValue == null ? default : InitialValue();
        Apply(Add(offset!, Valuer(0)));
        return offset!;
    }

    protected override void ApplyStep(T start, float time) {
        Apply(Add(start, Valuer(time)));
    }

    protected override void ApplyEnd(T start) {
        Apply(Add(start, Valuer(Time)));
    }
}
}