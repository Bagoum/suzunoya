using System;
using System.Linq.Expressions;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor.Reflection;

//this doesn't implement IMethodDesignation because we don't want it to report TypeDesignation at this level,
// as that would result in all invocations of the same method sharing the same type variables
//instead, TypeDesignation is copied at the InvokedMethod level, preventing cross-contamination
public interface IMethodSignature {
    /// <summary>
    /// Get a representation of this method's type. This should not directly be used for unification, as
    ///  its variable types should not be shared between all invocations.
    ///  Call <see cref="TypeDesignation.RecreateVariables"/> before using for unification.
    /// <br/>Note that lifted methods do NOT return a lifted type here. The types here are *unlifted*
    ///  over the TExArgCtx->TEx&lt;&gt; functor.
    /// <br/>Note that instance methods should prepend the instance type at the beginning of the argument array.
    /// </summary>
    TypeDesignation.Dummy SharedType { get; }
    
    /// <inheritdoc cref="MethodFlags"/>
    MethodFlags Flags { get; }
    
    /// <summary>
    /// Simplified description of the method parameters. Lifted methods have lifted parameters.
    /// </summary>
    NamedParam[] Params { get; }
    
    /// <summary>Executable information for the method/constructor/field/property.</summary>
    TypeMember Member { get; }

    /// <summary>
    /// True if this is a fallthrough method (BDSL1 only).
    /// </summary>
    bool IsFallthrough => false;

    /// <summary>
    /// True if this method is a constructor.
    /// </summary>
    bool IsCtor => false;
    
    /// <summary>
    /// True if this is a static method.
    /// </summary>
    bool IsStatic { get; }
    
    /// <summary>
    /// The return type of this method.
    /// <br/>Lifted methods return a lifted return type.
    /// </summary>
    Type ReturnType { get; }
    
    /// <summary>
    /// The type declaring this method.
    /// </summary>
    Type? DeclaringType { get; }
    
    /// <summary>
    /// Show the signature of this method.
    /// </summary>
    string AsSignature { get; }
    
    /// <summary>
    /// Show the signature of this method, including type restrictions.
    /// </summary>
    string AsSignatureWithRestrictions { get; }

    [PublicAPI]
    public string AsSignatureWithParamMod(Func<NamedParam, int, string> paramMod);
    
    /// <summary>
    /// Show the signature of this method, only including types and not names.
    /// </summary>
    string TypeOnlySignature { get; }

    InvokedMethod Call(string? calledAs);

    /// <summary>
    /// Get an attribute defined on the method.
    /// </summary>
    T? GetAttribute<T>() where T : Attribute;
    

    public ScopedConversionKind ImplicitTypeConvKind =>
        GetAttribute<ExpressionBoundaryAttribute>() != null ?
            ScopedConversionKind.BlockScopedExpression :
            ScopedConversionKind.Trivial;

    /// <summary>
    /// Invoke this method. If this is an instance method, the instance should be the first argument of `args`.
    /// </summary>
    object? Invoke(object?[] args);
    
    /// <summary>
    /// Invoke this method. If this is an instance method, the instance should be the first argument of `args`.
    /// </summary>
    Ex InvokeEx(params Ex[] args);
    
    /// <summary>
    /// Return the invocation of this method as an expression node,
    /// but if all arguments are constant, then instead call the method and wrap it in Ex.Constant.
    /// </summary>
    public Ex InvokeExIfNotConstant(params Ex[] args) {
        for (int ii = 0; ii < args.Length; ++ii)
            if (args[ii] is not ConstantExpression)
                return InvokeEx(args);
        var cargs = new object[args.Length];
        for (int ii = 0; ii < args.Length; ++ii)
            cargs[ii] = ((ConstantExpression)args[ii]).Value;
        return Ex.Constant(Invoke(cargs));
    }

    /// <summary>
    /// If this method is defined in a file, make a link to the file.
    /// </summary>
    string? MakeFileLink(string typName);

    /// <summary>
    /// (Informational) The name of the type declaring this method.
    /// </summary>
    string TypeName { get; }
    
    /// <summary>
    /// (Informational) The name of this method.
    /// </summary>
    /// <returns></returns>
    string Name { get; }
}

