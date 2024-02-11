using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BagoumLib.Expressions;
using JetBrains.Annotations;

namespace BagoumLib.Reflection {
/// <summary>
/// Helpers for reflection and type handling.
/// </summary>
[PublicAPI]
public static class ReflectionUtils {
    public static readonly Type[] FuncTypesByArity = {
        typeof(Func<>),
        typeof(Func<,>),
        typeof(Func<,,>),
        typeof(Func<,,,>),
        typeof(Func<,,,,>),
        typeof(Func<,,,,,>),
        typeof(Func<,,,,,,>),
        typeof(Func<,,,,,,,>),
        typeof(Func<,,,,,,,,>),
        typeof(Func<,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,,,,>),
    };
    public static readonly Type[] TupleTypesByArity = {
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>),
    };

    /// <summary>
    /// Get the func type typeof(Func&lt;,,,,...&gt;), where arity is the number of generic arguments.
    /// </summary>
    /// <param name="arity"></param>
    /// <returns></returns>
    public static Type GetFuncType(int arity) {
        if (arity <= 0 || arity > FuncTypesByArity.Length)
            throw new Exception($"Func type arity not supported: {arity}");
        return FuncTypesByArity[arity - 1];
    }

    /// <summary>
    /// Make the func type typeof(Func&lt;A,B,C,...&gt;).
    /// </summary>
    public static Type MakeFuncType(Type[] typeArgs) {
        return GetFuncType(typeArgs.Length).MakeGenericType(typeArgs);
    }
    
    /// <summary>
    /// Get the func type typeof(ValueTuple&lt;,,,,...&gt;), where arity is the number of generic arguments.
    /// </summary>
    /// <param name="arity"></param>
    /// <returns></returns>
    public static Type GetTupleType(int arity) {
        if (arity <= 0 || arity > TupleTypesByArity.Length)
            throw new Exception($"Tuple type arity not supported: {arity}");
        return TupleTypesByArity[arity - 1];
    }
    
    
    /// <summary>
    /// Alias for CSharpTypePrinter.Default.Print(t), which prints the type as close to the native
    /// C# description as possible without namespaces.
    /// </summary>
    public static string RName(this Type t) => CSharpTypePrinter.Default.Print(t);

    /// <summary>
    /// Returns true iff either t is equal to parent, or t is a strict subclass of parent.
    /// </summary>
    public static bool IsWeakSubclassOf(this Type? t, Type? parent) =>
        t == parent || (parent != null && t?.IsSubclassOf(parent) is true);
    
    
    /// <summary>
    /// Check if a realized type (eg. List&lt;List&lt;int&gt;&gt;)
    /// and a generic type (eg. List&lt;List&lt;T&gt;&gt;, where T is a generic method type)
    /// match. If they do, also get a dictionary mapping each generic type to its realized type.
    /// </summary>
    public static bool ConstructedGenericTypeMatch(Type realized, Type generic, out Dictionary<Type, Type> mapper) {
        mapper = new Dictionary<Type, Type>();
        return _ConstructedGenericTypeMatch(realized, generic, mapper);
    }

    /// <summary>
    /// Create a concrete method from a generic method provided a dictionary mapping generic types to concrete types.
    /// </summary>
    public static MethodInfo MakeGeneric(this MethodInfo mi, Dictionary<Type, Type> typeMap) {
        var typeParams = mi.GetGenericArguments()
            .Select(t => typeMap.TryGetValue(t, out var prm) ? prm : 
                throw new Exception($"Method {mi.Name} does not have enough information to construct a generic"));
        return mi.MakeGenericMethod(typeParams.ToArray());
    }

    /// <inheritdoc cref="ConstructedGenericTypeMatch(System.Type,System.Type,out System.Collections.Generic.Dictionary{System.Type,System.Type})"/>
    public static bool ConstructedGenericTypeMatch(IEnumerable<(Type realized, Type generic)> types,
        out Dictionary<Type, Type> mapper) {
        mapper = new Dictionary<Type, Type>();
        foreach (var (realized, generic) in types)
            if (!_ConstructedGenericTypeMatch(realized, generic, mapper))
                return false;
        return true;
    }

    private static bool _ConstructedGenericTypeMatch(Type realized, Type generic, Dictionary<Type, Type> genericMap) {
        if (generic.IsGenericParameter) {
            if (genericMap.TryGetValue(generic, out var x) && x != realized) return false;
            genericMap[generic] = realized;
            return true;
        }
        if (realized.IsGenericType != generic.IsGenericType) return false;
        if (!realized.IsGenericType) return realized == generic;
        return realized.GetGenericTypeDefinition() == generic.GetGenericTypeDefinition()
               && realized.GetGenericArguments().Length == generic.GetGenericArguments().Length
               && realized.GetGenericArguments()
                   .Zip(generic.GetGenericArguments(), (x, y) => (x, y))
                   .All(t => _ConstructedGenericTypeMatch(t.Item1, t.Item2, genericMap));
    }

    /// <summary>
    /// Create an instance of type T using its constructor.
    /// </summary>
    public static T CreateInstance<T>(params object[] args) => (T) Activator.CreateInstance(typeof(T), args)!;
    
    /// <summary>
    /// Find a property.
    /// </summary>
    /// <param name="t">Type containing the property</param>
    /// <param name="prop">Property name</param>
    /// <param name="instance">True if this is an instance property</param>
    public static PropertyInfo PropertyInfo(this Type t, string prop, bool instance=true) =>
        t.GetProperty(prop, (instance ? BindingFlags.Instance : BindingFlags.Static) 
                            | BindingFlags.NonPublic | BindingFlags.Public) ??
        throw new Exception($"Property {t.Name}.{prop} not found");
    
    /// <summary>
    /// Find a field.
    /// </summary>
    /// <param name="t">Type containing the field</param>
    /// <param name="field">Field name</param>
    /// <param name="instance">True if this is an instance field</param>
    public static FieldInfo FieldInfo(this Type t, string field, bool instance=true) =>
        t.GetField(field, (instance ? BindingFlags.Instance : BindingFlags.Static) 
                            | BindingFlags.NonPublic | BindingFlags.Public) ??
        throw new Exception($"Field {t.Name}.{field} not found");
    
    public static T _Property<T>(this object obj, string prop) => (T) (obj.GetType()
        .GetProperty(prop, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(obj)
    ?? throw new Exception($"{prop}<{typeof(T)}> not found"));
    
    
    public static T _StaticProperty<T>(this Type t, string prop) => (T) (t
        .GetProperty(prop, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null)
    ?? throw new Exception($"static {prop}<{typeof(T)}> not found"));
    
    public static T _Field<T>(this object obj, string prop) => (T) (obj.GetType()
        .GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(obj)
    ?? throw new Exception($"{prop}<{typeof(T)}> not found"));
    
    public static T _StaticField<T>(this Type t, string prop) => (T) (t
        .GetField(prop, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null)
    ?? throw new Exception($"static {prop}<{typeof(T)}> not found"));
}
}