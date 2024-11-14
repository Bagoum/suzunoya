using BagoumLib.Reflection;
using BagoumLib.Unification;

namespace Scriptor.Reflection;

/// <summary>
/// A description of a method called in reflection.
/// </summary>
public record InvokedMethod(IMethodSignature Mi, string? CalledAs) : IMethodDesignation {
    /// <summary>
    /// Method details.
    /// </summary>
    public IMethodSignature Mi { get; } = Mi;
    //important to ensure that multiple invocations don't share the same variable bindings!
    /// <inheritdoc/>
    public TypeDesignation.Dummy Method { get; } = 
        (Mi.SharedType.RecreateVariables() as TypeDesignation.Dummy)!;
    
    /// <summary>
    /// The name by which the user called the method (which may be an alias).
    /// </summary>
    public string? CalledAs { get; } = CalledAs;
    public NamedParam[] Params => Mi.Params;
    public string SimpleName {
        get {
            var prefix = Mi.IsCtor ? Mi.TypeName : Mi.Name;
            return (CalledAs == null || CalledAs.ToLower() == Mi.Name.ToLower()) ?
                prefix : $"{prefix}/{CalledAs}";
        }
    }
    public string Name => 
        Mi.IsCtor ? 
            $"new {Mi.TypeName}" :
            (CalledAs == null || CalledAs.ToLower() == Mi.Name.ToLower()) ? 
                Mi.Name : 
                $"{Mi.Name}/{CalledAs}";
    public string TypeEnclosedName => 
        Mi.IsCtor ?
            Name :
            $"{Mi.TypeName}.{Name}";

    public string FileLink =>
        Mi.MakeFileLink(TypeEnclosedName) ?? TypeEnclosedName;

    /// <inheritdoc/>
    public override string ToString() => Mi.AsSignature;
}

/// <summary>
/// See <see cref="LiftedMethodSignature"/>
/// </summary>
public record LiftedInvokedMethod(LiftedMethodSignature FMi, string? CalledAs) : InvokedMethod(FMi, CalledAs) {
    /// <inheritdoc/>
    public override string ToString() => Mi.AsSignature;
}

/// <summary>
/// See <see cref="LiftedMethodSignature{T,R}"/>
/// </summary>
public record LiftedInvokedMethod<T, R>(LiftedMethodSignature<T, R> TypedFMi, string? CalledAs) : LiftedInvokedMethod(TypedFMi, CalledAs) {
    /// <inheritdoc/>
    public override string ToString() => Mi.AsSignature;
}
