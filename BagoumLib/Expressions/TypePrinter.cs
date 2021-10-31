using System;
using System.Collections.Generic;
using System.Linq;

namespace BagoumLib.Expressions {
public interface ITypePrinter {
    public string Print(Type t);
}
/// <summary>
/// Tries really hard to convert a type to its C# representation, eg. "int[]" or "Func&lt;bool, string&gt;".
/// </summary>
public class CSharpTypePrinter : ITypePrinter {
    public Func<Type, bool> PrintTypeNamespace { get; set; } = _ => false;
    public string Print(Type t) {
        if (SimpleTypeNameMap.TryGetValue(t, out var v))
            return v;
        var ns = t.DeclaringType != null ?
            Print(t.DeclaringType) + "." :
            PrintTypeNamespace(t) ?
                t.Namespace + "." :
                "";
        if (t.IsArray)
            return $"{ns}{Print(t.GetElementType()!)}[]";
        if (t.IsConstructedGenericType)
            return $"{ns}{Print(t.GetGenericTypeDefinition())}" +
                   $"{ns}<{string.Join(", ", t.GenericTypeArguments.Select(Print))}>";
        if (t.IsGenericType) {
            int cutFrom = t.Name.IndexOf('`');
            if (cutFrom > 0)
                return ns + t.Name.Substring(0, cutFrom);
        }
        return ns + t.Name;
    }
    
    public static readonly Dictionary<Type, string> SimpleTypeNameMap = new() {
        {typeof(bool), "bool"},
        {typeof(byte), "byte"},
        {typeof(sbyte), "sbyte"},
        {typeof(char), "char"},
        {typeof(decimal), "decimal"},
        {typeof(double), "double"},
        {typeof(float), "float"},
        {typeof(int), "int"},
        {typeof(uint), "uint"},
        {typeof(nint), "nint"},
        {typeof(nuint), "nuint"},
        {typeof(long), "long"},
        {typeof(ulong), "long"},
        {typeof(short), "short"},
        {typeof(ushort), "ushort"},
        {typeof(object), "object"},
        {typeof(string), "string"},
    };
}

}