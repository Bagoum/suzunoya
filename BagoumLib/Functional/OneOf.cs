using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace BagoumLib.Functional {
/// <summary>
/// A value that is one of three possible types.
/// </summary>
[PublicAPI]
public readonly struct OneOf<A,B,C> {
    /// <summary>
    /// The index of the correct type (0=A, 1=B, 2=C).
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// True iff the value is of type A.
    /// </summary>
    [JsonIgnore]
    public bool IsA => Index == 0;

    /// <summary>
    /// True iff the value is of type B.
    /// </summary>
    [JsonIgnore]
    public bool IsB => Index == 1;

    /// <summary>
    /// True iff the value is of type C.
    /// </summary>
    [JsonIgnore]
    public bool IsC => Index == 2;

    /// <summary>
    /// The A-value of this type. Only valid if <see cref="IsA"/> is true.
    /// </summary>
    public A ValA { get; } = default!;
    
    /// <summary>
    /// The B-value of this type. Only valid if <see cref="IsB"/> is true.
    /// </summary>
    public B ValB { get; } = default!;
    
    /// <summary>
    /// The C-value of this type. Only valid if <see cref="IsC"/> is true.
    /// </summary>
    public C ValC { get; } = default!;
    
    
    /// <summary>
    /// Create a <see cref="OneOf{A,B,C}"/>.
    /// </summary>
    public OneOf(A val) {
        Index = 0;
        ValA = val;
    }
    /// <summary>
    /// Create a <see cref="OneOf{A,B,C}"/>.
    /// </summary>
    public OneOf(B val) {
        Index = 1;
        ValB = val;
    }
    /// <summary>
    /// Create a <see cref="OneOf{A,B,C}"/>.
    /// </summary>
    public OneOf(C val) {
        Index = 2;
        ValC = val;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is OneOf<A,B,C> other && Equals(other);
    
    /// <summary>
    /// Equality operator. Tests that both objects have the same left/right orientation, and that their valid values are the same.
    /// </summary>
    public bool Equals(OneOf<A,B,C> other) => this == other;

    /// <inheritdoc/>
    public override int GetHashCode() => Index switch {
        0 => (0, ValA).GetHashCode(),
        1 => (1, ValB).GetHashCode(),
        _ => (2, ValC).GetHashCode()
    };
    
    /// <inheritdoc cref="Equals(OneOf{A,B,C})"/>
    public static bool operator ==(in OneOf<A,B,C> a, in OneOf<A,B,C> b) =>
        (a.IsA && b.IsA && EqualityComparer<A>.Default.Equals(a.ValA, b.ValA)) ||
        (a.IsB && b.IsB && EqualityComparer<B>.Default.Equals(a.ValB, b.ValB)) ||
        (a.IsC && b.IsC && EqualityComparer<C>.Default.Equals(a.ValC, b.ValC));

    /// <summary>
    /// Inverted equality operator.
    /// </summary>
    public static bool operator !=(in OneOf<A,B,C> a, in OneOf<A,B,C> b) => !(a == b);

    /// <inheritdoc/>
    public override string ToString() => Index switch {
        0 => $"A<{ValA}>",
        1 => $"B<{ValB}>",
        _ => $"C<{ValC}>"
    };
    
    /// <inheritdoc cref="OneOf{A,B,C}(A)"/>
    public static implicit operator OneOf<A,B,C>(A val) => new(val);
    /// <inheritdoc cref="OneOf{A,B,C}(B)"/>
    public static implicit operator OneOf<A,B,C>(B val) => new(val);
    /// <inheritdoc cref="OneOf{A,B,C}(C)"/>
    public static implicit operator OneOf<A,B,C>(C val) => new(val);
}
}