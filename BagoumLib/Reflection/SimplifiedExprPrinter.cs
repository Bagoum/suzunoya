using System;
using System.Collections.Generic;
using BagoumLib.Expressions;
using JetBrains.Annotations;

namespace BagoumLib.Reflection;

/// <summary>
/// Type printer allowing injection of type simplifiers before printing.
/// </summary>
[PublicAPI]
public class SimplifiedExprPrinter : CSharpTypePrinter {
    /// <summary>
    /// Default instance.
    /// </summary>
    public new static readonly SimplifiedExprPrinter Default = new()
        { PrintTypeNamespace = _ => false };

    private readonly List<Func<Type, Type?>> simplifiers = [];

    /// <summary>
    /// Add a simplifier which will be used to recursively convert any type before printing it.
    /// </summary>
    public void InjectSimplifier(Func<Type, Type?> simplifier) => simplifiers.Add(simplifier);
    
    /// <inheritdoc/>
    public override string Print(Type t) {
        foreach (var s in simplifiers)
            if (s(t) is { } st)
                return Print(st);
        return base.Print(t);
    }
}