using System;
using Scriptor.Analysis;
using Scriptor.Expressions;

namespace Scriptor.Reflection;

/// <summary>
/// Description of an argument to a delegate or lexical scope.
/// </summary>
public interface IDelegateArg {
    /// <summary>
    /// Create an <see cref="TExArgCtx.Arg"/> representing this argument, if it is at the
    ///  provided index in the arguments list.
    /// </summary>
    public TExArgCtx.Arg MakeTExArg(int index);
    
    /// <summary>
    /// Convert this argument into an implicit declaration.
    /// </summary>
    public ImplicitArgDecl MakeImplicitArgDecl();
    
    /// <summary>
    /// Name of the argument.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Type of function argument, on the level of typeof(float).
    /// </summary>
    public Type Type { get; }
}

/// <inheritdoc/>
public readonly struct DelegateArg : IDelegateArg {
    /// <inheritdoc/>
    public string Name { get; }
    /// <inheritdoc/>
    public Type Type { get; }
    private readonly bool isRef;
    private readonly bool priority;
    /// <inheritdoc cref="DelegateArg"/>
    public DelegateArg(string name, Type t, bool isRef=false, bool priority=false) {
        this.Name = name;
        this.Type = t;
        this.isRef = isRef;
        this.priority = priority;
    }
    /// <inheritdoc/>
    public TExArgCtx.Arg MakeTExArg(int index) => ExHelpers.MakeAnyArg(Type, Name ?? $"$_arg{index+1}", priority, isRef);
    /// <inheritdoc/>
    public ImplicitArgDecl MakeImplicitArgDecl() => new(default, Type, Name);
}

/// <inheritdoc/>
public readonly struct DelegateArg<T> : IDelegateArg {
    /// <inheritdoc/>
    public string Name { get; }
    /// <inheritdoc/>
    public Type Type => typeof(T);
    private readonly bool isRef;
    private readonly bool priority;
    /// <inheritdoc cref="DelegateArg{T}"/>
    public DelegateArg(string name, bool isRef=false, bool priority=false) {
        this.Name = name;
        this.isRef = isRef;
        this.priority = priority;
    }
    /// <inheritdoc/>
    public TExArgCtx.Arg MakeTExArg(int index) => ExHelpers.MakeArg<T>(Name ?? $"$_arg{index+1}", priority, isRef);
    /// <inheritdoc/>
    public ImplicitArgDecl MakeImplicitArgDecl() => new ImplicitArgDecl<T>(default, Name);
}
