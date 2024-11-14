using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using BagoumLib;
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
/// A basic implementation of <see cref="GlobalScope"/>.
/// </summary>
public class DefaultGlobalScope : GlobalScope {
    /// <summary>
    /// Types queried for extension methods.
    /// </summary>
    public static Type[] ExtensionTypes { get; } = [ typeof(Enumerable), typeof(ObservableExtensions),
        typeof(BagoumLib.Extensions), typeof(ArrayExtensions), typeof(IEnumExtensions),
        typeof(ListExtensions), typeof(DictExtensions), typeof(DictExtensions),
        typeof(NullableExtensions), typeof(EventExtensions) ];

    /// <inheritdoc/>
    public override TypeResolver Resolver { get; } = new(
        new SingletonToArrayConv(),
        FixedImplicitTypeConv<string, LString>.FromFn(x => x),
        FixedImplicitTypeConv<int,float>.FromFn(x => x, allowConst: true),
        FixedImplicitTypeConv<Vector2,Vector3>.FromFn(v2 => new(v2, 0), allowConst: true)
    );

    /// <inheritdoc/>
    public DefaultGlobalScope() : base(ExtensionTypes) { }

    /// <inheritdoc/>
    public override List<MethodSignature>? StaticMethodDeclaration(string name) => null;

    /// <inheritdoc/>
    public override IImplicitTypeConverter? GetConverterForCompiledExpressionType(Type compiledType) {
        return null; //no compiled expression types in default support
    }

    /// <inheritdoc/>
    public override IImplicitTypeConverter? TryFindLowPriorityConversion(TypeDesignation to, TypeDesignation from) {
        return null;
    }
}

/// <summary>
/// A basic implementation of <see cref="ILangCustomizer"/>.
/// </summary>
public class DefaultLangCustomizer: ILangCustomizer {
    /// <inheritdoc cref="DefaultLangCustomizer"/>
    public DefaultLangCustomizer() {
        GlobalScope.Singleton = new DefaultGlobalScope();
        ServiceLocator.Register<ILangCustomizer>(this);
    }
    
    /// <inheritdoc/>
    D ILangCustomizer.CompileDelegate<D>(Func<TExArgCtx, TEx> func, params IDelegateArg[] args) {
        var tac = new TExArgCtx(args.Select((a, i) => a.MakeTExArg(i)).ToArray());
        var body = func(tac);
        var prms = tac.Args.SelectNotNull(a => (Ex)a.expr as ParameterExpression).ToArray();
        return Ex.Lambda<D>(body, prms).Compile();
    }

    /// <inheritdoc/>
    Func<ST.Import, Either<EnvFrame, ReflectionException>> ILangCustomizer.Import { get; set; } =
        imp => new ReflectionException(imp.Position, "Import functionality not yet supported in default logic");

    /// <inheritdoc/>
    AOTMode ILangCustomizer.AOTMode => AOTMode.None;

    /// <inheritdoc/>
    LString? ILangCustomizer.TryFindLocalizedStringReference(string content) => null;

    //No autovars in the base language support
    
    /// <inheritdoc/>
    void ILangCustomizer.Declare(LexicalScope s, PositionRange p, int autoVarMethod) {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    void ILangCustomizer.Extend(LexicalScope s, PositionRange p, int autoVarExtend, string? key) {
        throw new NotImplementedException();
    }

    //No lambda-like or scope-aware objects in the base language support
    
    /// <inheritdoc/>
    void ILangCustomizer.AttachEFToLambdaLikeObject(AST ast, Unifier u) { }

    /// <inheritdoc/>
    Ex ILangCustomizer.AttachScopeToScopeAwareObject(TExArgCtx tac, Ex prm, Type typ, LexicalScope scope) => prm;

    /// <inheritdoc/>
    DocumentSymbol? ILangCustomizer.CustomSymbolTree(IDebugAST ast) => null;

    /// <inheritdoc/>
    SemanticToken ILangCustomizer.FromMethod(IMethodSignature mi, PositionRange p, string? tokenType, Type? retType) {
        return new(p, tokenType ?? SemanticTokenTypes.MethodType(mi));
    }
}