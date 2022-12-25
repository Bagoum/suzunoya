using System;
using System.Collections.Generic;
using System.Linq;

namespace BagoumLib.Expressions {
/// <summary>
/// An interface that prints a type into a string.
/// </summary>
public interface ITypePrinter {
    /// <summary>
    /// Print a type.
    /// </summary>
    public string Print(Type t);
}
/// <summary>
/// Tries really hard to convert a type to its C# representation, eg. "int[]" or "Func&lt;bool, string&gt;".
/// </summary>
public class CSharpTypePrinter : ITypePrinter {
    public static readonly ITypePrinter Default = new CSharpTypePrinter();
    public Func<Type, bool> PrintTypeNamespace { get; init; } = _ => false;

    private static readonly Type[] tupleTypes = {
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>)
    };
    
    /// <inheritdoc/>
    public virtual string Print(Type t) {
        if (SimpleTypeNameMap.TryGetValue(t, out var v))
            return v;
        string PrependEnclosure(string s) {
            if (t.IsGenericParameter) return s;
            return t.DeclaringType != null ?
                $"{Print(t.DeclaringType)}.{s}" :
                PrintTypeNamespace(t) && (t.Namespace?.Length > 0) ?
                    $"{t.Namespace}.{s}" :
                    s;
        }
        if (t.IsArray)
            return PrependEnclosure($"{Print(t.GetElementType()!)}[]");
        if (t.IsConstructedGenericType) {
            var gt = t.GetGenericTypeDefinition();
            var args = string.Join(", ", t.GenericTypeArguments.Select(Print));
            return PrependEnclosure(tupleTypes.Contains(gt) ? 
                $"({args})" : 
                $"{Print(gt)}<{args}>");
        }
        if (t.IsGenericType) {
            int cutFrom = t.Name.IndexOf('`');
            if (cutFrom > 0)
                return PrependEnclosure(t.Name[..cutFrom]);
        }
        return PrependEnclosure(t.Name);
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