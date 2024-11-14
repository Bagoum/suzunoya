using BagoumLib.Reflection;
using JetBrains.Annotations;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using Scriptor.Compile;
using Position = LanguageServer.VsCode.Contracts.Position;

namespace Scriptor.Analysis;

/// <summary>
/// If a type T implements this interface, the type T[] will be flattened when generating document symbols.
/// </summary>
public interface IFlattenDocSymbolArray { }

/// <summary>
/// Helper functions for analyzing ASTs, consumed by the language server.
/// </summary>
[PublicAPI]
public static class LangServHelpers {
    /// <summary>
    /// Get the <see cref="SymbolKind"/> for a <see cref="TypeMember"/>.
    /// </summary>
    public static SymbolKind Symbol(this TypeMember mem) => mem switch {
        TypeMember.Constructor => SymbolKind.Constructor,
        TypeMember.Property => SymbolKind.Property,
        TypeMember.Field f => f.Fi.DeclaringType == f.Fi.FieldType && f.Fi.DeclaringType?.IsEnum is true ? 
            SymbolKind.Enum : SymbolKind.Field,
        _ => SymbolKind.Method,
    };
    
    /// <summary>
    /// Convert a script position into a VSCode position.
    /// </summary>
    public static Position ToPosition(this Mizuhashi.Position pos) =>
        new(pos.Line - 1, pos.Column - 1);

    /// <summary>
    /// Convert a VSCode position into a script position.
    /// </summary>
    public static Mizuhashi.Position ToDMKPosition(this Position pos, string source) =>
        new(source, pos.Line + 1, pos.Character + 1);

    /// <summary>
    /// Get a script position range encompassing a set of ASTs.
    /// </summary>
    public static PositionRange? ToRange(this IDebugAST?[] asts) {
        if (asts.Length == 0) return null;
        Mizuhashi.Position? nmin = null;
        Mizuhashi.Position? nmax = null;
        for (int ii = 0; ii < asts.Length; ++ii) {
            if (asts[ii] is {} ast) {
                if (nmin is not {} min || ast.Position.Start.Index < min.Index)
                    nmin = ast.Position.Start;
                if (nmax is not {} max || ast.Position.End.Index > max.Index)
                    nmax = ast.Position.End;
            }
        }
        return (nmin is { } _min && nmax is { } _max) ? new(_min, _max) : null;
    }
    
    /// <summary>
    /// Convert a script position range into a VSCode position range.
    /// </summary>
    public static Range ToRange(this PositionRange pos) =>
        new(pos.Start.ToPosition(), pos.End.ToPosition());

    /// <summary>
    /// Convert a VSCode position range into a script position range.
    /// </summary>
    public static PositionRange ToDMKRange(this Range r, string source) =>
        new(r.Start.ToDMKPosition(source), r.End.ToDMKPosition(source));
    
    /// <summary>
    /// Convert a parsing error into a VSCode diagnostic.
    /// </summary>
    public static Diagnostic ToDiagnostic<T>(this LocatedParserError err, InputStream<T> stream) => 
        new(DiagnosticSeverity.Error, 
            stream.TokenWitness.ToPosition(err.Index, stream.Source.Length).ToRange(), 
            "Parsing", stream.ShowAllFailures(err));
}