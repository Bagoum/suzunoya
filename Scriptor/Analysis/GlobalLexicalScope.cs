using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Scriptor.Reflection;

namespace Scriptor.Analysis;

/// <summary>
/// A global scope used for base lexical scope resolution.
/// </summary>
public abstract class GlobalScope : LexicalScope {
    /// <summary>
    /// Singleton instance of the global scope.
    /// </summary>
    public static GlobalScope Singleton { get; set; } = null!;

    /// <inheritdoc cref="TypeResolver"/>
    public abstract TypeResolver Resolver { get; }

    /// <summary>
    /// Enum values that can be referenced without the enum type. 
    /// </summary>
    public Dictionary<string, List<(Type, object)>> EnumResolver { get; } = new();

    /// <summary>
    /// Recorded extension methods.
    /// </summary>
    protected readonly Dictionary<Type, Dictionary<string, List<MethodSignature>>> extensionMethods;

    /// <inheritdoc cref="GlobalScope"/>
    public GlobalScope(Type[] extensionTypes) {
        GlobalRoot = this;
        VariableDecls = Array.Empty<(Type, VarDecl[])>();
        extensionMethods = FindExtensionMethods(extensionTypes);
    }

    /// <summary>
    /// Configure support for an enum in <see cref="EnumResolver"/>.
    /// </summary>
    public void ConfigureEnum<E>((string key, E value)[] vals) {
        Type e = typeof(E);
        foreach (var (first, value) in vals) {
            EnumResolver.AddToList(first, (e, value!));
        }
    }
    
    /// <summary>
    /// Find a static method by name.
    /// </summary>
    public abstract List<MethodSignature>? StaticMethodDeclaration(string name);

    private IEnumerable<MethodSignature> ExtensionsForResolvedOrTypeConstructor(Type t, string? name) =>
        extensionMethods.TryGetValue(t, out var map) ?
            (name is null ? map.Values.SelectMany(x => x) :
                map.TryGetValue(name, out var named) ? named : Array.Empty<MethodSignature>()) :
            Array.Empty<MethodSignature>();
    
    /// <summary>
    /// Find all extension methods on a given type, filtering by name if provided.
    /// </summary>
    public virtual IEnumerable<MethodSignature> ExtensionMethods(Type instTyp, string? name) {
        foreach (var ms in ExtensionsForResolvedOrTypeConstructor(instTyp, name))
            yield return ms;
        if (instTyp.IsConstructedGenericType)
            foreach (var ms in ExtensionsForResolvedOrTypeConstructor(instTyp.GetGenericTypeDefinition(), name))
                yield return ms;
    }

    /// <summary>
    /// Try to find a conversion between the two types. Checks <see cref="GetConverterForCompiledExpressionType"/>
    ///  as well as conversion rules in <see cref="Resolver"/>. Used when casting the output type of a script.
    /// </summary>
    public IImplicitTypeConverter? TryFindConversion(TypeDesignation to, TypeDesignation from) {
        var invoke = TypeDesignation.Dummy.Method(to, from);
        if (to.Resolve().LeftOrNull is { } toT && GetConverterForCompiledExpressionType(toT) is { } exCompiler)
            return exCompiler.NextInstance.MethodType.Unify(invoke, Unifier.Empty).IsLeft ? exCompiler : null;
        if (Resolver.GetImplicitSources(to, out var convs))
            foreach (var c in convs)
                if (c.NextInstance.MethodType.Unify(invoke, Unifier.Empty).IsLeft)
                    return c;
        
        if (Resolver.GetImplicitCasts(from, out convs))
            foreach (var c in convs)
                if (c.NextInstance.MethodType.Unify(invoke, Unifier.Empty).IsLeft)
                    return c;
        return null;
    }
    
    /// <summary>
    /// If the provided type is a compiled expression type, then return a type converter that compiles an expression.
    /// </summary>
    public abstract IImplicitTypeConverter? GetConverterForCompiledExpressionType(Type compiledType);
    
    /// <summary>
    /// Try looking for a low-priority type conversion between the two types.
    /// Used when casting the output type of a script, only if <see cref="TryFindConversion"/> fails.
    /// <br/>Low-priority conversions should not be mirrored in <see cref="Resolver"/>.
    /// </summary>
    public abstract IImplicitTypeConverter? TryFindLowPriorityConversion(TypeDesignation to, TypeDesignation from);
    
    internal override Either<Unit, IDeclaration> _Declare(IDeclaration decl) =>
        throw new Exception("Do not declare variables in GlobalScope");

    /// <summary>
    /// Lookup all extension methods in the provided classes.
    /// </summary>
    protected static Dictionary<Type, Dictionary<string, List<MethodSignature>>> FindExtensionMethods(Type[] typs) {
        var meths = new Dictionary<Type, Dictionary<string, List<MethodSignature>>>();
        foreach (var t in typs)
        foreach (var mi in t.GetMethods())
            if (MethodSignature.Get(mi) is { Member: TypeMember.Method { IsExtension: true } } sig) {
                var thisTd = sig.SharedType.Arguments[0];
                //NB: if thisTd is generic, eg. Dict<K,V>, then it cannot be resolved but is Known
                if ((thisTd.Resolve().LeftOrNull ?? (thisTd as TypeDesignation.Known)?.Typ) is { } thisTyp)
                    meths.AddToList2(thisTyp, sig.Name, sig);
            }
        return meths;
    }
}
