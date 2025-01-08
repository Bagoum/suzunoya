using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using JetBrains.Annotations;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using Scriptor.Analysis;
using Scriptor.Expressions;

namespace Scriptor.Compile;

/// <summary>
/// An AST providing debug information.
/// </summary>
[PublicAPI]
public interface IDebugAST : IDebugPrint {
    /// <summary>
    /// Position of the code that will generate this object.
    /// <br/>This is used for debugging/logging/error messaging.
    /// </summary>
    PositionRange Position { get; }
    
    /// <inheritdoc cref="IAST.Params"/>
    IEnumerable<IDebugAST> Children { get; }
    
    /// <summary>
    /// Return the furthest-down AST in the tree that encloses the given position,
    /// then its ancestors up to the root.
    /// <br/>Each element is paired with the index of the child that preceded it,
    ///  or null if it is the lowermost AST returned.
    /// <br/>Returns null if the AST does not enclose the given position.
    /// </summary>
    IEnumerable<(IDebugAST tree, int? childIndex)>? NarrowestASTForPosition(PositionRange p);
    
    /// <summary>
    /// Print out a readable, preferably one-line description of the AST (not including its children). Consumed by language server.
    /// </summary>
    [PublicAPI]
    string Explain();

    /// <summary>
    /// Return a parse tree for the AST. Consumed by language server.
    /// </summary>
    [PublicAPI]
    public DocumentSymbol ToSymbolTree(string? descr = null);

    /// <summary>
    /// Describe the semantics of all the parsed tokens in the source code.
    /// Consumed by language server.
    /// </summary>
    [PublicAPI]
    IEnumerable<SemanticToken> ToSemanticTokens();
}

/// <summary>
/// A lexed parsing token that has a purpose.
/// </summary>
/// <param name="Position">Position of the token in the source code</param>
/// <param name="TokenType">Token type</param>
/// <param name="TokenMods">Token modifiers</param>
[PublicAPI]
public record SemanticToken(PositionRange Position, string TokenType, IList<string>? TokenMods = null) {
    /// <summary>
    /// Create a semantic token that is the same as this, but is marked as constant.
    /// </summary>
    public SemanticToken WithConst(bool isConst) => isConst ?
        this with { TokenMods = (TokenMods ??[]).Append(BasicSemanticTokenModifiers.Const).ToArray() } :
        this;
}