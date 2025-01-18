using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Mizuhashi;
using Scriptor;
using Scriptor.Compile;
using Scriptor.Definition;
using Scriptor.Expressions;
using Scriptor.Reflection;

namespace Scriptor.Analysis;
/// <summary>
/// Flags for configuring how a declaration is looked up in lexical scopes.
/// </summary>
[Flags]
public enum DeclarationLookup {
    /// <summary>
    /// A declaration with a constant value (declared via const var).
    /// </summary>
    CONSTANT = 1 << 0,
    
    /// <summary>
    /// A lexically scoped declaration.
    /// </summary>
    LEXICAL_SCOPE = 1 << 1,
    
    /// <summary>
    /// A dynamically scoped declaration.
    /// </summary>
    DYNAMIC_SCOPE = 1 << 2,
    
    /// <inheritdoc cref="CONSTANT"/>
    ConstOnly = CONSTANT,
    /// <summary>
    /// Lexically scoped or constant declarations.
    /// </summary>
    Standard = ConstOnly | LEXICAL_SCOPE,
    /// <summary>
    /// Dynamically scoped, lexically scoped, or constant declarations.
    /// </summary>
    Dynamic = Standard | DYNAMIC_SCOPE,
}
/// <summary>
/// Type unification error for untyped variables.
/// </summary>
public record UntypedVariable(VarDecl Declaration) : TypeUnifyErr;
/// <summary>
/// Type unification error for variables with inferred type `void`.
/// </summary>
public record VoidTypedVariable(VarDecl Declaration) : TypeUnifyErr;

/// <summary>
/// Interface for declared variables, functions, or parameters.
/// </summary>
public interface IDeclaration {
    /// <summary>
    /// Position of the declaration.
    /// </summary>
    PositionRange Position { get; }
    
    /// <summary>
    /// Name of the declared object.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// True if this variable should be hoisted into the parent scope of the location where its declaration occured.
    /// </summary>
    bool Hoisted { get; }
    
    /// <summary>
    /// The scope in which this variable is declared. Assigned by <see cref="LexicalScope.Declare"/>
    /// </summary>
    LexicalScope DeclarationScope { get; set; }
    
    /// <summary>
    /// An optional comment describing the declaration.
    /// </summary>
    string? DocComment { get; set; }
    
    /// <summary>
    /// Try to finalize the type of this declaration. Called during lexical scope type finalization.
    /// </summary>
    Either<Unit, TypeUnifyErr> FinalizeType(Unifier u);

    /// <summary>
    /// Set a doc comment from the provided lexer metadata if applicable.
    /// </summary>
    public void TrySetDocComment(LexerMetadata comments) {
        foreach (var (p, c) in comments.Comments) {
            if (p.End.Line + 1 == Position.Start.Line) {
                //Space before newline required for VSCode newlining
                DocComment = c.Replace("\n", "  \n");
                return;
            } else if (p.End.Line >= Position.Start.Line)
                break;
        }
    }
}

/// <summary>
/// A declaration of a variable.
/// </summary>
public class VarDecl : IDeclaration {
    /// <inheritdoc/>
    public PositionRange Position { get; }
    
    /// <inheritdoc/>
    public bool Hoisted { get; }
    
    /// <summary>
    /// If true, then this declaration has a constant value.
    /// </summary>
    public bool Constant { get; set; }
    /// <summary>
    /// If <see cref="Constant"/> is true, then this stores the constant value of the declaration during execution.
    /// </summary>
    public Maybe<ConstantExpression> ConstantValue { get; set; } = Maybe<ConstantExpression>.None;
    
    /// <summary>
    /// The type of this variable as provided in the declaration. May be empty, in which case it will be inferred.
    /// </summary>
    public Type? KnownType { get; }
    
    /// <inheritdoc/>
    public string Name { get; }
    
    /// <summary>
    /// The type designation of this variable (either a known type or a variable that will be resolved).
    /// </summary>
    public TypeDesignation TypeDesignation { get; }
    
    /// <summary>
    /// If present, then this VarDecl is copied into an EnvFrame from the arguments of a function.
    /// </summary>
    public ImplicitArgDecl? SourceImplicit { get; }

    /// <inheritdoc/>
    public LexicalScope DeclarationScope { get; set; } = null!;
    
    /// <inheritdoc/>
    public string? DocComment { get; set; }

    /// <summary>
    /// The expression pointing to this variable (null if the scope is an Ex.Block scope).
    /// </summary>
    private ParameterExpression? _parameter;
    
    /// <summary>
    /// The final non-ambiguous type of this variable. Only present after <see cref="FinalizeType"/> is called.
    /// </summary>
    public TypeDesignation? FinalizedTypeDesignation { get; protected set; }
    
    /// <summary>
    /// The final non-ambiguous type of this variable. Only present after <see cref="FinalizeType"/> is called.
    /// </summary>
    public Type? FinalizedType { get; protected set; }
    
    /// <summary>
    /// The index of this declaration's type among the types of all declarations in the same <see cref="LexicalScope"/>.
    /// <br/>The ordering of indexes is arbitrary and may not correspond to the order of declarations.
    /// <br/>Assigned by <see cref="LexicalScope.FinalizeVariableTypes"/>.
    /// </summary>
    public int TypeIndex { get; set; }
    
    /// <summary>
    /// The index of this declaration among all declarations
    ///  with the same <see cref="FinalizedType"/> declared in the same <see cref="LexicalScope"/>.
    /// <br/>The ordering of indexes is arbitrary and may not correspond to the order of declarations.
    /// <br/>Assigned by <see cref="LexicalScope.FinalizeVariableTypes"/>.
    /// </summary>
    public int Index { get; set; }
    
    /// <summary>
    /// The number of times this declaration receives an assignment.
    /// <br/>Set during the AST <see cref="IAST.Verify"/> step.
    /// <br/>This should always be at least 1.
    /// </summary>
    public int Assignments { get; set; }
    
    /// <summary>
    /// A declaration of a variable.
    /// </summary>
    public VarDecl(PositionRange Position, bool Hoist, Type? knownType, string Name, ImplicitArgDecl? sourceImplicit = null) {
        this.Position = Position;
        this.Hoisted = Hoist;
        this.KnownType = knownType;
        this.Name = Name;
        this.SourceImplicit = sourceImplicit;
        if (knownType != null) {
            TypeDesignation = TypeDesignation.FromType(knownType);
            FinalizedType = knownType;
        } else {
            TypeDesignation = sourceImplicit?.TypeDesignation ??
                              new TypeDesignation.Variable();
        }
    }

    /// <summary>
    /// If this is a local variable for an Ex.Block (rather than an implicit variable),
    /// return the parameter definition.
    /// </summary>
    public virtual ParameterExpression? DeclaredParameter(TExArgCtx tac) =>
        DeclarationScope.UseEF ? null : _parameter ??= Expression.Variable(
                FinalizedType ?? 
                throw new ReflectionException(Position, $"Variable declaration {Name} not finalized"), Name);

    /// <summary>
    /// Get the read/write expression representing this variable, where `envFrame`'s scope is the scope in which
    ///  this variable was declared (and tac.EnvFrame is the frame of usage).
    /// </summary>
    public virtual Expression Value(Expression? envFrame, TExArgCtx tac) =>
        DeclarationScope.UseEF ?
            EnvFrame.Value(envFrame ?? throw new CompileException(
                $"The variable `{Name}` (declared at {Position}) is stored in the environment frame, "+
                "but is accessed from a callsite that has no environment frame."), 
                Expression.Constant(TypeIndex), Expression.Constant(Index), FinalizedType!) :
            _parameter ?? throw new StaticException($"{nameof(Value)} called before {nameof(DeclaredParameter)}");
    
    /// <inheritdoc/>
    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        FinalizedTypeDesignation = TypeDesignation.Simplify(u);
        var td = FinalizedTypeDesignation.Resolve(u);
        if (td.IsRight)
            return new UntypedVariable(this);
        FinalizedType = td.Left;
        if (FinalizedType == typeof(void))
            return new VoidTypedVariable(this);
        return SourceImplicit?.FinalizeType(u) ?? Unit.Default;
    }

    /// <summary>
    /// Show this declaration in the format `Type Name`.
    /// </summary>
    public string AsParam => $"{FinalizedType?.SimpRName()} {Name}";
}

/// <summary>
/// A declaration implicitly provided through TExArgCtx or a fixed expression.
/// </summary>
public class ImplicitArgDecl : VarDecl, IDelegateArg {
    Type IDelegateArg.Type => FinalizedType!;
    /// <inheritdoc/>
    public override ParameterExpression? DeclaredParameter(TExArgCtx tac) => null;
    
    /// <inheritdoc/>
    public override Expression Value(Expression? envFrame, TExArgCtx tac) =>
        tac.GetByName(FinalizedType ?? 
                      throw new CompileException("Cannot retrieve ImplicitArgDecl before type is finalized"), Name);

    /// <inheritdoc cref="ImplicitArgDecl"/>
    public ImplicitArgDecl(PositionRange Position, Type? knownType, string Name) : base(Position, false, knownType,
        Name) {
        ++Assignments;
    }
    
    /// <inheritdoc/>
    public virtual TExArgCtx.Arg MakeTExArg(int index) => ExHelpers.MakeAnyArg(
        FinalizedType ?? throw new Exception("Implicit arg declaration type not yet finalized"), Name, false, false);
    
    /// <inheritdoc/>
    public ImplicitArgDecl MakeImplicitArgDecl() => this;
    
    /// <inheritdoc/>
    public override string ToString() => $"{Name}<{FinalizedType?.SimpRName()}>";
}

/// <inheritdoc cref="ImplicitArgDecl"/>
public class ImplicitArgDecl<T> : ImplicitArgDecl {
    /// <inheritdoc/>
    public ImplicitArgDecl(PositionRange Position, string Name) : base(Position, typeof(T), Name) { }
    /// <inheritdoc/>
    public override TExArgCtx.Arg MakeTExArg(int index) => ExHelpers.MakeArg<T>(Name, false, false);
}

/// <summary>
/// Declaration of a script function.
/// </summary>
public class ScriptFnDecl : IDeclaration {
    /// <inheritdoc/>
    public string Name { get; init; }
    /// <inheritdoc/>
    public bool Hoisted { get; }
    /// <summary>
    /// Arguments to the script function.
    /// </summary>
    public ImplicitArgDecl[] Args { get; }
    /// <summary>
    /// Default values of script function arguments.
    /// </summary>
    public IAST?[] Defaults { get; }
    /// <summary>
    /// AST representing the script function.
    /// </summary>
    public AST.ScriptFunctionDef Tree { get; set; }
    /// <summary>
    /// Type of this script function.
    /// </summary>
    public TypeDesignation.Dummy CallType { get; private set; }
    
    /// <summary>
    /// If true, then invocations of this function should be extracted as constants.
    /// </summary>
    public bool IsConstant { get; init; }
    /// <inheritdoc/>
    public PositionRange Position => Tree.Position;
    /// <inheritdoc/>
    public LexicalScope DeclarationScope { get; set; } = null!;
    /// <inheritdoc/>
    public string? DocComment { get; set; }
    /// <summary>
    /// The type of this script function as a Func or Action.
    /// </summary>
    public Type? FuncType { get; private set; }
    private object? _compiled = null;
    private bool isCompiling = false;
    private Expression? requiresReplace;
    
    /// <inheritdoc cref="ScriptFnDecl"/>
    public ScriptFnDecl(AST.ScriptFunctionDef Tree, bool Hoist, string Name, ImplicitArgDecl[] Args, IAST?[] Defaults, TypeDesignation.Dummy CallType) {
        this.Name = Name;
        this.Hoisted = Hoist;
        this.Args = Args;
        this.Defaults = Defaults;
        this.Tree = Tree;
        this.CallType = CallType;
    }
    
    /// <summary>
    /// Compile this script function into a delegate and return an expression pointing to it.
    /// </summary>
    public Expression Compile(TExArgCtx tac) {
        if (_compiled is null) {
            if (isCompiling) {
                //recursive function
                requiresReplace ??= Expression.Constant(this).Field(nameof(_compiled)).As(FuncType!);
                if (tac.Ctx.BakeTracker is ExBakeTracker.Save save)
                    save.RecursiveFunctions[requiresReplace] = this;
                return requiresReplace;
            }
            isCompiling = true;
            FuncType ??= Tree.CompileFuncType();
            _compiled = Tree.CompileFunc(FuncType);
            isCompiling = false;
        }
        return Expression.Constant(_compiled);
    }

    /// <summary>
    /// The return type of this function.
    /// </summary>
    public Type? ReturnType => Tree.Body.LocalScope!.Return!.FinalizedType;

    /// <summary>
    /// Print the signature of this function, in the format `ReturnType Name(Type1 Arg1, Type2 Arg2...)`
    /// </summary>
    public string AsSignature(string? namePrefix = null) {
        var name = namePrefix is null ? Name : $"{namePrefix}.{Name}";
        return $"{ReturnType?.SimpRName()} {name}({string.Join(", ", Args.Select(a => a.AsParam))})";
    }

    /// <summary>
    /// Print the signature of this function, in the format `(Type1, Type2...): ReturnType`
    /// </summary>
    public string TypeOnlySignature =>
        $"({string.Join(", ", Args.Select(a => a.FinalizedType?.SimpRName()))}): {ReturnType?.SimpRName()}";

    /// <inheritdoc/>
    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        CallType = CallType.SimplifyDummy(u);
        //For now we don't need any particular resolution here since the implicit args
        // and block should resolve all variables. However, we still do need to 
        // simplify CallType in case this function is referenced in another script.
        return Unit.Default;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Tree.Position} {AsSignature()}";
}

/// <summary>
/// Definition of a macro.
/// </summary>
public record MacroDecl(ST.MacroDef Tree, string Name, string[] Args, ST?[] Defaults) : IDeclaration {
    /// <inheritdoc/>
    public PositionRange Position => Tree.Position;
    /// <inheritdoc/>
    public bool Hoisted => false;
    /// <inheritdoc/>
    public LexicalScope DeclarationScope { get; set; } = null!;
    /// <inheritdoc/>
    public string? DocComment { get; set; }
    
    /// <inheritdoc/>
    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) => Unit.Default;
}

/// <summary>
/// An import declaration.
/// </summary>
public record ScriptImport(PositionRange Position, EnvFrame Ef, string FileKey, string? ImportFrom, string? ImportAs) : IDeclaration {
    /// <inheritdoc/>
    public string Name => ImportAs ?? FileKey;
    /// <inheritdoc/>
    public bool Hoisted => false;
    /// <inheritdoc/>
    public LexicalScope DeclarationScope { get; set; } = null!;
    /// <inheritdoc/>
    public string? DocComment { get; set; }
    
    /// <inheritdoc/>
    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) => Unit.Default;
}

/// <summary>
/// Interface for automatic variable declarations in lexical scope. Should be extended by customizers.
/// </summary>
public interface IAutoVars;
