using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BagoumLib.Expressions {
public interface IObjectPrinter {
    public string Print(object o); 
}

/// <summary>
/// Tries really hard to convert an object to a C# representation.
/// <br/>Note that this is not possible for all types.
/// </summary>
public class CSharpObjectPrinter : IObjectPrinter {
    public CSharpTypePrinter TypePrinter { get; set; } = new CSharpTypePrinter();

    private static readonly HashSet<Type> CastTypes = new() {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort)
    };
    public string Print(object? o) {
        if (o == null)
            return "null";
        var typ = o.GetType();
        if (typ.IsArray) {
            var arr = (o as Array)!;
            return $"new {TypePrinter.Print(typ.GetElementType()!)}[] {{ " +
                   $"{string.Join(", ", Enumerable.Range(0, arr.Length).Select(i => Print(arr.GetValue(i))))} }}";
        }
        if (CastTypes.Contains(typ))
            return $"(({TypePrinter.Print(typ)}){o})";
        return FormattableString.Invariant(o switch {
            int i => $"{i}",
            bool b => b ? $"true" : $"false",
            double d => $"{d}d",
            float f => $"{f}f",
            uint u => $"{u}u",
            long l => $"{l}L",
            ulong ul => $"{ul}uL",
            decimal dec => $"{dec}m",
            char c => $"'{c}'",
            string s => $"\"{s.Replace("\"", "\\\"")}\"",
            Exception e => $"new {TypePrinter.Print(e.GetType())}({Print(e.Message)})",
            Type t => $"typeof({TypePrinter.Print(t)})",
            { } obj => throw new Exception($"Couldn't print object {obj} of type {typ}")
        });
    }
}



}