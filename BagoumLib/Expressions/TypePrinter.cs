using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

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
[PublicAPI]
public class CSharpTypePrinter : ITypePrinter {
    /// <summary>
    /// Default printer with <see cref="UseSimpleTypeNames"/> = true, <see cref="PrintTypeNamespace"/> = false.
    /// </summary>
    public static readonly ITypePrinter Default = new CSharpTypePrinter();
    /// <summary>
    /// If true, will represent simple system types like int/Int32 as "int" instead of "System.Int32".
    /// </summary>
    public bool UseSimpleTypeNames { get; init; } = true;
    
    /// <summary>
    /// Whether or not to print the namespace of types.
    /// </summary>
    public Func<Type, bool> PrintTypeNamespace { get; init; } = _ => false;

    /// <summary>
    /// The non-empty tuple types ordered by arity.
    /// </summary>
    public static readonly Type[] tupleTypes = {
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>)
    };
    
    /// <summary>
    /// Map of simple types, such as int/Int32, to their simple names, such as "int"
    /// </summary>
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
        {typeof(ulong), "ulong"},
        {typeof(short), "short"},
        {typeof(ushort), "ushort"},
        {typeof(object), "object"},
        {typeof(string), "string"},
        {typeof(void), "void"}
    };
    
    /// <inheritdoc/>
    public virtual string Print(Type t) {
        if (UseSimpleTypeNames && SimpleTypeNameMap.TryGetValue(t, out var v))
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
            return $"{Print(t.GetElementType()!)}[]";
        if (t.IsConstructedGenericType) {
            var gt = t.GetGenericTypeDefinition();
            var args = string.Join(", ", t.GenericTypeArguments.Select(Print));
            return tupleTypes.Contains(gt) ? 
                $"({args})" : 
                $"{Print(gt)}<{args}>";
        }
        if (t.IsGenericType) {
            int cutFrom = t.Name.IndexOf('`');
            if (cutFrom > 0)
                return PrependEnclosure(t.Name[..cutFrom]);
        }
        return PrependEnclosure(t.Name);
    }
    
}

}