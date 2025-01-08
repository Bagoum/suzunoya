using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Scriptor.Analysis;
using Scriptor.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor;

/// <summary>
/// Extensions for type manipulation in reflection.
/// </summary>
public static class Extensions {
    /// <summary>
    /// Render the doc comment for a declaration for language server consumption.
    /// </summary>
    public static string DocCommentDisplay(this IDeclaration x) => x.DocComment is { } c ? $"  \n*{c}*" : "";

    
    //public static TEx EnvFrame(this TExArgCtx tac) => tac.GetByType<EnvFrame>();
    
    /// <summary>
    /// Get a simple description of the type `t`.
    /// </summary>
    public static string SimpRName(this TypeDesignation t) {
        if (t is TypeDesignation.Known kt) {
            if (kt.Typ == typeof(Func<,>) && (kt.Arguments[0] as TypeDesignation.Known)?.Typ == typeof(TExArgCtx)) {
                return SimpRName(kt.Arguments[1]);
            } else if (kt.Typ == typeof(TEx<>))
                return SimpRName(kt.Arguments[0]);
            if (kt.IsArrayTypeConstructor)
                return SimpRName(kt.Arguments[0]) + "[]";
            if (kt.Arguments.Length == 0)
                return kt.Typ.SimpRName();
            if (ReflectionUtils.TupleTypesByArity.Contains(kt.Typ))
                return $"({string.Join(", ", kt.Arguments.Select(SimpRName))})";
            return kt.Typ.SimpRName() + $"<{string.Join(",", kt.Arguments.Select(SimpRName))}>";
        } else if (t is TypeDesignation.Dummy d) {
            return $"({string.Join(",", d.Arguments.Take(d.Arguments.Length - 1).Select(SimpRName))})->{SimpRName(d.Last)}";
        } else if (t is TypeDesignation.Variable { RestrictedTypes: { } rt })
            return $"{string.Join(" or ", rt.Select(SimpRName).Distinct())}";
        else
            return t.ToString()!;
    }
    
    
    /// <summary>
    /// If this type is of the form TEx&lt;R&gt; or TExArgCtx->TEx&lt;R&gt;, then return true and set inner to R.
    /// <br/>If this type is TEx or TExArgCtx->TEx, then return true and set inner to void.
    /// </summary>
    public static bool IsTExOrTExFuncType(this Type t, out Type inner) {
        if (t.IsTExType(out inner))
            return true;
        if (t.IsTExFuncType(out inner))
            return true;
        inner = t;
        return false;
    }

    /// <summary>
    /// If this type is of the form TEx&lt;R&gt;, then return true and set inner to R.
    /// <br/>If this type is TEx, then return true and set inner to void.
    /// </summary>
    public static bool IsTExType(this Type t, out Type inner) {
        if (t == typeof(TEx)) {
            inner = typeof(void);
            return true;
        } else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(TEx<>)) {
            inner = t.GetGenericArguments()[0];
            return true;
        }
        inner = t;
        return false;
    }
    
    /// <summary>
    /// If this type is of the form TExArgCtx->TEx&lt;R&gt;, then return true and set inner to R.
    /// <br/>If this type is TExArgCtx->TEx, then return true and set inner to void.
    /// </summary>
    public static bool IsTExFuncType(this Type t, out Type inner) {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Func<,>)) {
            var gargs = t.GetGenericArguments();
            if (gargs[0] == typeof(TExArgCtx) && gargs[1].IsTExType(out inner)) {
                return true;
            }
        }
        inner = t;
        return false;
    }

    private static readonly Dictionary<Type, (Type, ConstructorInfo)> texTypeCache = new();
    
    /// <summary>
    /// For a type T, get the type TEx&lt;T&gt; and its constructor.
    /// </summary>
    public static (Type type, ConstructorInfo exConstructor) GetTExType(this Type simpleType) {
        if (texTypeCache.TryGetValue(simpleType, out var texTyp))
            return texTyp;
        var tt = typeof(TEx<>).MakeGenericType(simpleType);
        return texTypeCache[simpleType] = (tt, tt.GetConstructor([typeof(Ex)])!);
    }

    /// <summary>
    /// Cast an expression to the type TEx&lt;T&gt;. (If t is void, then cast the expression to type TEx.)
    /// </summary>
    public static TEx MakeTypedTEx(this Type t, Ex ex) {
        if (t == typeof(void)) return ex;
        var (_, cons) = t.GetTExType();
        return (cons.Invoke([ex]) as TEx)!;
    }

    /// <summary>
    /// Retype the provided function as TExArgCtx -> TEx{T}. (If t is void, don't retype it.)
    /// </summary>
    public static Func<TExArgCtx, TEx> MakeTypedLambda(this Type t, Func<TExArgCtx, TEx> f) =>
        TExLambdaTyper.ConvertForType(t, f);
}

/// <summary>
/// Convert Func&lt;TExArgCtx, TEx&gt; to Func&lt;TExArgCtx, TEx&lt;T&gt;&gt;.
/// </summary>
public static class TExLambdaTyper {
    private static readonly Dictionary<Type, MethodInfo> converters = new();
    private static readonly MethodInfo cmi = typeof(TExLambdaTyper).GetMethod(nameof(Convert))!;

    /// <summary>
    /// Convert Func&lt;TExArgCtx, TEx&gt; to Func&lt;TExArgCtx, TEx&lt;T&gt;&gt; for a compile-time
    ///  known type T.
    /// </summary>
    public static Func<TExArgCtx, TEx<T>> Convert<T>(Func<TExArgCtx, TEx> f) => tac => (Ex)f(tac);
    
    /// <summary>
    /// Convert Func&lt;TExArgCtx, TEx&gt; to Func&lt;TExArgCtx, TEx&lt;T&gt;&gt; for a dynamically
    ///  determined type t using reflection.
    /// </summary>
    public static Func<TExArgCtx, TEx> ConvertForType(Type t, Func<TExArgCtx, TEx> f) {
        if (t == typeof(void)) return f;
        var conv = converters.TryGetValue(t, out var c) ? c : converters[t] = cmi.MakeGenericMethod(t);
        return (conv.Invoke(null, new object[] { f }) as Func<TExArgCtx, TEx>)!;
    }
}