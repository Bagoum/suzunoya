using System;
using System.Reflection;
using BagoumLib.Culture;
using BagoumLib.Functional;
using BagoumLib.Unification;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Expressions;
using Scriptor.Reflection;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor.Compile;

/// <summary>
/// An external provider for language customization and expression-to-delegate compilation.
/// </summary>
public interface ILangCustomizer {
    /// <inheritdoc cref="CompileDelegate"/>
    D CompileDelegate<D>(Func<TExArgCtx, TEx> func, params IDelegateArg[] args) where D : Delegate;
    
    private static readonly GenericMethodSignature CompileDelegateMeth = (GenericMethodSignature)
        MethodSignature.Get(typeof(ILangCustomizer)
            .GetMethod(nameof(CompileDelegate), BindingFlags.Public|BindingFlags.Instance)!);
    
    /// <summary>
    /// Compile a delegate from an expression.
    /// </summary>
    internal object CompileDelegate(Type delType, Func<TExArgCtx, TEx> func, params IDelegateArg[] args) =>
        CompileDelegateMeth.Specialize(delType).Invoke(this, func, args)!;
    
    /// <summary>
    /// Execute an import configured in <see cref="ST.Import"/>.
    /// </summary>
    Func<ST.Import, Either<EnvFrame, ReflectionException>> Import { get; set; }
    
    /// <inheritdoc cref="Scriptor.AOTMode"/>
    AOTMode AOTMode { get; }

    /// <summary>
    /// Try to retrieve the localized string with the key configured by `content`.
    /// </summary>
    LString? TryFindLocalizedStringReference(string content);
    
    /// <summary>
    /// Auto-declare variables in a lexical scope based on an int key.
    /// </summary>
    void Declare(LexicalScope s, PositionRange p, int autoVarMethod);
    
    /// <summary>
    /// Auto-extend declared variables in a lexical scope based on an int key.
    /// </summary>
    void Extend(LexicalScope s, PositionRange p, int autoVarExtend, string? key);
    
    /// <summary>
    /// If this AST has a local scope, then modify the highest non-assign method calls returning a lambda-like object
    ///  immediately within this local scope such that the executing EnvFrame is attached to them.
    /// <br/>In DMK, lambda-like types are StateMachine, AsyncPattern, and SyncPattern.
    /// </summary>
    void AttachEFToLambdaLikeObject(AST ast, Unifier u);

    /// <summary>
    /// For a method call defining a local scope,
    ///  attach that local scope to the values constructed by direct children if they are scope-aware.
    /// <br/>In DMK, scope-aware types are StateMachine and GenCtxProperties.
    /// </summary>
    Ex AttachScopeToScopeAwareObject(TExArgCtx tac, Ex prm, Type typ, LexicalScope scope);
    
    /// <summary>
    /// Implement custom logic for generating a symbol tree in the language server.
    /// </summary>
    DocumentSymbol? CustomSymbolTree(IDebugAST ast);

    /// <summary>
    /// Create a semantic token for a method invocation.
    /// </summary>
    SemanticToken FromMethod(IMethodSignature mi, PositionRange p, string? tokenType = null, Type? retType = null);
}