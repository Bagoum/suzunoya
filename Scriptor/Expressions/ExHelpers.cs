using System;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor.Expressions;

/// <summary>
/// Helper methods for expressions.
/// </summary>
[PublicAPI]
public static class ExHelpers {
    /// <summary>
    /// typeof(float)
    /// </summary>
    public static readonly Type tfloat = typeof(float);
    
    /// <inheritdoc cref="MakeAnyArg"/>
    public static TExArgCtx.Arg MakeArg<T>(string name, bool hasTypePriority, bool isRef = false) {
        var expr = TEx.MakeParameter<T>(isRef, name);
        return TExArgCtx.Arg.FromTEx(name, expr, hasTypePriority);
    }

    /// <summary>
    /// Configure an argument for <see cref="TExArgCtx"/>.
    /// </summary>
    public static TExArgCtx.Arg MakeAnyArg(Type t, string name, bool hasTypePriority, bool isRef = false) =>
        (TExArgCtx.Arg)makeArg.MakeGenericMethod(t).Invoke(null, new object[] { name, hasTypePriority, isRef })!;

    private static readonly MethodInfo makeArg = typeof(ExHelpers).GetMethod(nameof(MakeArg))!;
    
    /// <summary>
    /// If `body` is a constant, then cast it to type `toTyp` and return a constant expression.
    /// </summary>
    public static Ex? TryFlattenConversion(Ex body, Type toTyp) {
        //Null typecasts don't work correctly with nullable types (including nullable struct type)
        //Object boxing doesn't always work with value types
        if (body.TryAsAnyConst(out var obj) && obj != null && toTyp != typeof(object) && !toTyp.Name.StartsWith("Nullable")) {
            return Ex.Constant(Ex.Lambda(Ex.Convert(Ex.Constant(obj), toTyp)).Compile().DynamicInvoke());
        }
        return null;
    }
    
    /// <summary>
    /// Create a float variable.
    /// </summary>
    public static ParameterExpression VFloat(string? name = null) => Ex.Variable(tfloat, name);
    
    /// <summary>
    /// Return an exception if the provided expression is not writeable.
    /// <br/>Implementation based on https://source.dot.net/#System.Linq.Expressions/System/Linq/Expressions/Expression.cs,241
    /// </summary>
    public static NotWriteableException? AssertWriteable(int argIndex, object prm) {
        NotWriteableException Err(string err) => new(argIndex, err);
        Expression ex;
        try {
            if (prm is TEx tex)
                ex = tex;
            else
                ex = (Ex)prm;
        } catch (Exception ecx) {
            return new(argIndex, $"Failure: this is not an expression. Please report this.", ecx);
        }
        switch (ex) {
            case IndexExpression iex:
                if (iex.Indexer?.CanWrite is false)
                    return Err("This is an indexing expression, but the indexer is not writeable.");
                return null;
            case MemberExpression mex:
                return mex.Member switch {
                    PropertyInfo p => p.CanWrite ?
                        null :
                        Err($"The property {p.Name} is not writeable."),
                    FieldInfo f => !(f.IsInitOnly || f.IsLiteral) ?
                        null :
                        Err($"The field {f.Name} is not writeable."),
                    { } m => Err($"Member {m.Name} is of an unhandled type {m.GetType().SimpRName()}")
                };
            case ParameterExpression:
                return null;
            default:
                return Err($"This expression has type {ex.GetType().SimpRName()}:{ex.NodeType}, which is not writeable." +
                           " A writeable expression is an array indexer, property, field, or variable.");
            
        }
        
    }

}