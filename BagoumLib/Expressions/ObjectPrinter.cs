using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace BagoumLib.Expressions {
public interface IObjectPrinter {
    public string Print(object? o); 
}

/// <summary>
/// Tries really hard to convert an object to a C# representation.
/// <br/>Note that this is not possible for all types.
/// </summary>
public class CSharpObjectPrinter : IObjectPrinter {
    public CSharpTypePrinter TypePrinter { get; set; } = new CSharpTypePrinter();

    /// <summary>
    /// Set this to true to use .ToString() if the value cannot be printed.
    /// </summary>
    public bool FallbackToToString { get; init; } = false;

    private static readonly HashSet<Type> CastTypes = new() {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort)
    };

    private static string PrintChar(char c) => c switch {
        '\0' => "\\0",
        '\a' => "\\a",
        '\b' => "\\b",
        '\f' => "\\f",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        '\v' => "\\v",
        '\\' => "\\\\",
        '"' => "\\\"",
        '\'' => "\\\'",
        _ => $"{c}",
    };
    public virtual string Print(object? o) {
        if (o == null)
            return "null";
        var typ = o.GetType();
        if (typ.IsArray) {
            var arr = (o as Array)!;
            return $"new {TypePrinter.Print(typ.GetElementType()!)}[] {{ " +
                   $"{string.Join(", ", Enumerable.Range(0, arr.Length).Select(i => Print(arr.GetValue(i))))} }}";
        }
        if (typ.IsConstructedGenericType && typ.GetGenericTypeDefinition() == typeof(List<>)) {
            var arr = (o as IList)!;
            return $"new List<{TypePrinter.Print(typ.GetGenericArguments()[0])}>() {{" +
                   $"{string.Join(", ", Enumerable.Range(0, arr.Count).Select(i => Print(arr[i])))} }}";
        }
        if (o is ITuple tup) {
            return $"({string.Join(", ", Enumerable.Range(0, tup.Length).Select(i => Print(tup[i])))})";
        }
        if (typ.IsEnum)
            return $"{TypePrinter.Print(typ)}.{o.ToString()}";
        if (CastTypes.Contains(typ))
            return $"(({TypePrinter.Print(typ)}){o})";
        return FormattableString.Invariant(o switch {
            int i => $"{i}",
            bool b => b ? (FormattableString)$"true" : $"false",
            double d => $"{d}d",
            float f => $"{f}f",
            uint u => $"{u}u",
            long l => $"{l}L",
            ulong ul => $"{ul}uL",
            decimal dec => $"{dec}m",
            char c => $"'{PrintChar(c)}'",
            string s => $"\"{string.Join("", s.Select(PrintChar))}\"",
            Exception e => $"new {TypePrinter.Print(e.GetType())}({Print(e.Message)})",
            Type t => $"typeof({TypePrinter.Print(t)})",
            { } obj => NoPrintMethodFallback(obj)
        });
    }

    /// <summary>
    /// Method called when no standard method of printing the object succeeded.
    /// </summary>
    protected virtual FormattableString NoPrintMethodFallback(object obj) =>
        FallbackToToString ?
            (FormattableString)$"{obj.ToString()}" :
            throw new Exception($"Couldn't print object {obj} of type {obj.GetType()}");
}



}