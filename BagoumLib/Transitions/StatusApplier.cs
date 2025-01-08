using System;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using static BagoumLib.Mathematics.GenericOps;

namespace BagoumLib.Transitions {
/// <summary>
/// A transition that sets a value based on a function of time (<see cref="Valuer"/>).
/// </summary>
/// <typeparam name="T"></typeparam>
public record StatusApplier<T> : TransitionBase<T> {
    //----- Required variables
    
    /// <summary>
    /// Value to apply given time T.
    /// If <see cref="InitialValue"/> is defined, this will be added to it.
    /// </summary>
    public Func<float, T> Valuer { get; init; }
    
    //----- Required variables with defaults provided

    //-----Optional variables set via initializer syntax (or possibly fluent API)

    /// <summary>
    /// If present, will be used as an offset for Valuer.
    /// Evaluated when the transition is run.
    /// </summary>
    public Func<T>? InitialValue = null;
    
    
    private static readonly Func<T, T, T> Add = GetAddOp<T>().add;
    
    /// <inheritdoc cref="StatusApplier{T}"/>
    public StatusApplier(Func<float, T> value, float time, Action<T> apply, ICancellee? cT = null) {
        Valuer = value;
        Time = time;
        Apply = apply;
        CToken = cT;
    }

    /// <inheritdoc/>
    protected override T ApplyStart() {
        var offset = InitialValue == null ? default : InitialValue();
        Apply(Add(offset!, Valuer(0)));
        return offset!;
    }

    /// <inheritdoc/>
    protected override void ApplyStep(T start, float time) {
        Apply(Add(start, Valuer(time)));
    }

    /// <inheritdoc/>
    protected override void ApplyEnd(T start) {
        Apply(Add(start, Valuer(Time)));
    }
}
}