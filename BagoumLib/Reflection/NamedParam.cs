using System;
using System.Reflection;
using BagoumLib.Expressions;
using JetBrains.Annotations;

namespace BagoumLib.Reflection;

/// <summary>
/// A simplified description of a method parameter.
/// </summary>
/// <param name="Type">Parameter type</param>
/// <param name="Name">Parameter name</param>
[PublicAPI]
public readonly record struct NamedParam(Type Type, string Name) {
    /// <summary>
    /// Implicit conversion of ParameterInfo to <see cref="NamedParam"/>.
    /// </summary>
    public static implicit operator NamedParam(ParameterInfo pi) => 
        new(pi.ParameterType, pi.Name);

    /// <summary>
    /// Description of this type in the format "TYPE NAME", where TYPE is simplified.
    /// </summary>
    public string AsParameter => $"{Type.SimpRName()} {Name}";
}
