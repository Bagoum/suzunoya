using System.Collections;
using System.Linq;
using System.Text;
using BagoumLib.Reflection;
using Scriptor.Reflection;

namespace Scriptor.Analysis;

/// <summary>
/// Helpers for reading debug information from language constructs.
/// </summary>
public static class DebugHelpers {
    /// <summary>
    /// Debug the contents of an EnvFrame.
    /// </summary>
    public static void DebugRootEf(StringBuilder sb, EnvFrame ef) {
        DebugEF(sb, ef, null);
        foreach (var (key, imp) in ef.Scope.ImportDecls) {
            sb.Append($"Import {imp.Name}:\n");
            DebugEF(sb, imp.Ef, key);
        }
    }

    private static void DebugEF(StringBuilder sb, EnvFrame ef, string? asImport) {
        void Header() {
            if (asImport != null) sb.Append("\t");
        }
        void Indent() => sb.Append(asImport != null ? "\t\t" : "\t");
        Header();
        sb.Append("Variables:\n");
        foreach (var vdecl in ef.Scope.AllVisibleVars.Where(v => !v.Name.StartsWith('$'))) {
            Indent();
            var vName = $"{vdecl.Name}<{vdecl.FinalizedType?.RName()}>";
            if (asImport != null)
                vName = $"{asImport}.{vName}";
            if (vdecl.ConstantValue.Try(out var v))
                sb.Append($"(const) {vName}: {Print(v.Value)}\n");
            else
                sb.Append($"{vName}: {Print(efVal.Specialize(vdecl.FinalizedType!).Invoke(ef, vdecl))}\n");
        }
        Header();
        sb.Append("Functions:\n");
        foreach (var fndecl in ef.Scope.AllVisibleScriptFns) {
            Indent();
            if (fndecl.IsConstant)
                sb.Append("(const) ");
            sb.Append(fndecl.AsSignature(asImport) + "\n");
        }
    }
    
    /// <summary>
    /// Print a simple representation of an object for debugging purposes.
    /// </summary>
    public static string Print(object? o) {
        if (o is null) return "<null>";
        if (o is IEnumerable ie) {
            var frags = from object? x in ie select Print(x);
            return $"{{ {string.Join(", ", frags)} }}";
        }
        return o.ToString() ?? "<unknown>";
    }
    
    private static readonly GenericMethodSignature efVal = (GenericMethodSignature)MethodSignature.Get(typeof(EnvFrame)
        .GetMethod(nameof(EnvFrame.NonRefValue), [typeof(VarDecl)])!);
}