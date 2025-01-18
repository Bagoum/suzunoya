using System;
using BagoumLib;
using BagoumLib.Functional;
using JetBrains.Annotations;
using Scriptor.Analysis;
using Scriptor.Definition;
using Scriptor.Expressions;
using Scriptor.Reflection;

namespace Scriptor.Compile;

/// <summary>
/// A script function that returns a value of type T.
/// </summary>
public delegate T ScriptFn<T>(out EnvFrame rootEf);

/// <summary>
/// A script function whose return value is ignored.
/// </summary>
public delegate void ErasedScriptFn(out EnvFrame rootEf);

/// <summary>
/// Helpers for end-to-end script compilation.
/// </summary>
[PublicAPI]
public static class CompileHelpers {
    /// <summary>
    /// `out EnvFrame $scriptEf` argument.
    /// </summary>
    public static IDelegateArg OutEnvFrameArg => new DelegateArg<EnvFrame>("$scriptEf", true);
    
    /// <summary>
    /// (Stage 0) Convert the provided script into an ST.
    /// </summary>
    public static ST.Block Parse(ref string source, out LexerMetadata metadata) {
        var tokens = Lexer.Lex(ref source, out metadata);
        var parse = LangParser.Parse(source, tokens, out var stream);
        if (parse.IsRight) 
            throw new ReflectionException(stream.TokenWitness.ToPosition(parse.Right.Index, parse.Right.End), 
                stream.ShowAllFailures(parse.Right));
        return parse.Left;
    }
    
    /// <summary>
    /// (Stage 1) Convert the provided script into an ST, then annotate it into an AST.
    /// <br/>This does not perform typechecking (<see cref="Typecheck"/>).
    /// <br/>The AST may have errors (<see cref="IAST.FirstPassExceptions"/>). However, these can be thrown *after*
    ///  initial typechecking.
    /// </summary>
    /// <param name="source">Script code</param>
    /// <param name="args">Top-level script arguments</param>
    /// <returns></returns>
    /// <exception cref="ReflectionException">Thrown when the script could not be parsed or there are basic errors in the AST.</exception>
    public static (IAST, LexicalScope) ParseAnnotate(ref string source, params IDelegateArg[] args) {
        var parse = Parse(ref source, out var metadata);
        var scope = LexicalScope.NewTopLevelScope();
        var ast = parse.AnnotateWithParameters(new(scope), args).LeftOrRight<AST.Block, AST.Failure, IAST>();
        scope.SetDocComments(metadata);
        return (ast, scope);
    }

    /// <summary>
    /// (Stage 2) Typecheck the provided AST. 
    /// </summary>
    /// <param name="ast">The AST to typecheck (as returned by <see cref="ParseAnnotate"/>).</param>
    /// <param name="rootScope">The top-level scope of the AST (as returned by <see cref="ParseAnnotate"/>).</param>
    /// <param name="finalType">The required output type of the AST (eg. Vector3 or TP or StateMachine).</param>
    /// <param name="resultType">The actual output type of the AST (eg. Vector3 or TP or StateMachine).</param>
    /// <returns>The typechecked AST.</returns>
    /// <exception cref="Exception">Thrown when there are typechecking errors or if there are <see cref="IAST.FirstPassExceptions"/> errors.</exception>
    public static IAST Typecheck(this IAST ast, LexicalScope rootScope, Type? finalType, out Type resultType) {
        var typ = IAST.Typecheck(ast, rootScope.GlobalRoot.Resolver, rootScope, finalType);
        foreach (var exc in ast.FirstPassExceptions()) 
            throw exc;
        if (typ.IsRight)
            throw IAST.EnrichError(typ.Right);
        resultType = typ.Left;
        foreach (var d in ast.WarnUsage())
            d.Log();
        return ast;
    }
    
    /// <inheritdoc cref="IAST.Verify"/>
    public static IAST Finalize(this IAST ast) {
        foreach (var exc in ast.Verify())
            throw exc;
        return ast;
    }
    
    /// <summary>
    /// (Stage 4) Realize the AST into an expression function, then compile it into a delegate.
    /// </summary>
    public static D Compile<D>(this IAST ast, params IDelegateArg[] args) where D : Delegate {
        return ServiceLocator.Find<ILangCustomizer>().CompileDelegate<D>(ast.Realize, args);
    }
    
    /// <summary>
    /// Runs all four stages of script parsing (<see cref="ParseAnnotate"/>, <see cref="Typecheck"/>,
    /// <see cref="Finalize"/>, <see cref="Compile{D}"/>) on a script.
    /// </summary>
    public static D ParseAndCompileDelegate<D>(string source, params IDelegateArg[] args) where D : Delegate {
        var (ast, gs) = ParseAnnotate(ref source, args);
        var typechecked = ast.Typecheck(gs, typeof(D).GetMethod("Invoke")!.ReturnType, out _);
        var verified = typechecked.Finalize();
        return verified.Compile<D>(args);
    }

    /// <summary>
    /// Parse a script as `ScriptFn{T}`, then execute it and return the object + EF.
    /// </summary>
    public static (T, EnvFrame) ParseAndCompileValue<T>(string source) {
        var fn = ParseAndCompileDelegate<ScriptFn<T>>(source, OutEnvFrameArg);
        var ret = fn(out var ef);
        return (ret, ef);
    }

    /// <summary>
    /// Parse a script as `ErasedScriptFn`, then execute it and return the EF.
    /// </summary>
    public static EnvFrame ParseAndCompileErased(string source) {
        var fn = ParseAndCompileDelegate<ErasedScriptFn>(source,OutEnvFrameArg);
        fn(out var ef);
        return ef;
    }
}

