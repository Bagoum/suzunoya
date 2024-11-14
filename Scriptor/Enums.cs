using System;
using Scriptor.Reflection;

namespace Scriptor;

/// <summary>
/// The type of AOT compilation being used.
/// </summary>
public enum AOTMode {
    /// <summary>
    /// Not AOT. Expression compilation is fully allowed.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Pre-compiling expressions for eventual AOT usage.
    /// </summary>
    Save = 1,
    
    /// <summary>
    /// In an AOT context, loading pre-compiled expressions.
    /// </summary>
    Load = 2,
    
}

public enum ExMode {
    Parameter,
    RefParameter
}

/// <summary>
/// Flags attached to a <see cref="IMethodSignature"/>.
/// </summary>
[Flags]
public enum MethodFlags {
    /// <summary>
    /// No flags.
    /// </summary>
    None = 0,
    /// <summary>
    /// The method can be treated as a constant function in AOT or non-AOT contexts.
    /// <br/>If set to true, the return value of this method must be printable by expression-to-code converters.
    /// </summary>
    ConstableAny = 1 << 0,
    /// <summary>
    /// The method can be treated as a constant function in non-AOT contexts only.
    /// </summary>
    ConstableNonAOT = 1 << 1,
}

