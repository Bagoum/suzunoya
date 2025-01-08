using System.Collections.Generic;
using System.Linq;
using BagoumLib.Expressions;
using JetBrains.Annotations;

namespace Scriptor;

/// <summary>
/// An AST that can be printed in a readable manner.
/// </summary>
[PublicAPI]
public interface IDebugPrint {
    /// <summary>
    /// Print a readable description of the entire AST.
    /// </summary>
    public IEnumerable<PrintToken> DebugPrint();
    
    /// <inheritdoc cref="DebugPrint"/>
    string DebugPrintStringify() => new ExpressionPrinter().Stringify(DebugPrint().ToArray());
    
    /// <summary>
    /// Helper function for printing arguments with a separator.
    /// </summary>
    public static IEnumerable<PrintToken> PrintArgs(IReadOnlyList<IDebugPrint> args, string sep = ",") {
        if (args.Count > 1) {
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            for (int ii = 0; ii < args.Count; ++ii) {
                foreach (var x in args[ii].DebugPrint())
                    yield return x;
                if (ii < args.Count - 1) {
                    yield return sep;
                    yield return PrintToken.newline;
                }
            }
            yield return PrintToken.dedent;
            //yield return PrintToken.newline;
        } else if (args.Count == 1) {
            foreach (var x in args[0].DebugPrint())
                yield return x;
        }
    }
}