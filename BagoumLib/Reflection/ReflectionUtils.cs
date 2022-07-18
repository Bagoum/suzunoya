using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib.Expressions;
using JetBrains.Annotations;

namespace BagoumLib.Reflection {
[PublicAPI]
public static class ReflectionUtils {
    /// <summary>
    /// Alias for CSharpTypePrinter.Default.Print(t), which prints the type as close to the native
    /// C# description as possible without namespaces.
    /// </summary>
    public static string RName(this Type t) => CSharpTypePrinter.Default.Print(t);
    
    
    /// <summary>
    /// Check if a realized type (eg. List&lt;List&lt;int&gt;&gt;)
    /// and a generic type (eg. List&lt;List&lt;T&gt;&gt;, where T is a generic method type)
    /// match. If they do, also get a dictionary mapping each generic type to its realized type.
    /// </summary>
    public static bool ConstructedGenericTypeMatch(Type realized, Type generic, out Dictionary<Type, Type> mapper) {
        mapper = new Dictionary<Type, Type>();
        return _ConstructedGenericTypeMatch(realized, generic, mapper);
    }

    public static MethodInfo MakeGeneric(this MethodInfo mi, Dictionary<Type, Type> typeMap) {
        var typeParams = mi.GetGenericArguments()
            .Select(t => typeMap.TryGetValue(t, out var prm) ? prm : 
                throw new Exception($"Method {mi.Name} does not have enough information to construct a generic"));
        return mi.MakeGenericMethod(typeParams.ToArray());
    }

    public static bool _ConstructedGenericTypeMatch(Type realized, Type generic, Dictionary<Type, Type> genericMap) {
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


    public static T CreateInstance<T>(params object[] args) => (T) Activator.CreateInstance(typeof(T), args)!;
    
    public static MethodInfo MethodInfo(this Type t, string method, bool instance=true) =>
        t.GetMethod(method, (instance ? BindingFlags.Instance : BindingFlags.Static) 
                            | BindingFlags.NonPublic | BindingFlags.Public) ??
        throw new Exception($"Method {t.Name}.{method} not found");
    
    public static PropertyInfo PropertyInfo(this Type t, string prop, bool instance=true) =>
        t.GetProperty(prop, (instance ? BindingFlags.Instance : BindingFlags.Static) 
                            | BindingFlags.NonPublic | BindingFlags.Public) ??
        throw new Exception($"Property {t.Name}.{prop} not found");
    
    public static FieldInfo FieldInfo(this Type t, string prop, bool instance=true) =>
        t.GetField(prop, (instance ? BindingFlags.Instance : BindingFlags.Static) 
                            | BindingFlags.NonPublic | BindingFlags.Public) ??
        throw new Exception($"Field {t.Name}.{prop} not found");
    
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