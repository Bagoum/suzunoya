using System;
using System.Linq;
using System.Reflection;

namespace BagoumLib.Reflection {
public static class ReflectionUtils {

    public static string NameType(Type t) {
        if (t.IsArray) {
            return $"[{NameType(t.GetElementType()!)}]";
        }
        if (t.IsConstructedGenericType) {
            return
                $"{NameType(t.GetGenericTypeDefinition())}<{string.Join(", ", t.GenericTypeArguments.Select(NameType))}>";
        }
        if (t.IsGenericType) {
            var n = t.Name;
            int cutFrom = n.IndexOf('`');
            if (cutFrom > 0) return n.Substring(0, cutFrom);
        }
        return t.Name;
    }

    public static string RName(this Type t) => NameType(t);
    
    public static PropertyInfo _PropertyInfo(this object obj, string prop) =>
        obj.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
        throw new Exception($"{prop} not found");
    public static T _Property<T>(this object obj, string prop) => (T) (obj.GetType()
        .GetProperty(prop, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(obj)
    ?? throw new Exception($"{prop}<{typeof(T)}> not found"));
    
}
}