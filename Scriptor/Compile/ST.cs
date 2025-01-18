using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using Scriptor;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Definition;
using Scriptor.Expressions;
using Scriptor.Math;
using Scriptor.Reflection;
using Ex = System.Linq.Expressions.Expression;
using Position = Mizuhashi.Position;

namespace Scriptor.Compile {

/// <summary>
/// Record containing state information for annotating an ST into an AST.
/// </summary>
public record STAnnotater(LexicalScope Scope, Dictionary<string, ST>? VarReplace = null) {
    /// <summary>
    /// Annotate all the provided arguments with this annotater.
    /// </summary>
    public IAST[] AnnotateAll(ST[] args) {
        var results = new IAST[args.Length];
        for (int ii = 0; ii < args.Length; ++ii)
            results[ii] = args[ii].Annotate(this);
        return results;
    }
}

/// <summary>
/// A syntax tree formed by parsing.
/// <br/>The syntax tree has no knowledge of bindings or types that are not explicitly declared,
/// but it can be transformed into an <see cref="IAST"/> that does.
/// </summary>
public abstract record ST(PositionRange Position) : IDebugPrint {
    /// <summary>
    /// Diagnostics that should be applied to the resultant AST.
    /// </summary>
    public ReflectDiagnostic[] Diagnostics { get; init; } = [];

    private static SemanticToken? Token(PositionRange? pos, string type) => 
        pos.Try(out var p) ? new(p, type) : null;

    private static SemanticToken? Keyword(PositionRange? pos) => Token(pos, SemanticTokenTypes.Keyword);
    private static SemanticToken? Op(PositionRange? pos) => Token(pos, SemanticTokenTypes.Operator);
    private static SemanticToken? Type(PositionRange? pos) => Token(pos, SemanticTokenTypes.Type);
    
    /// <inheritdoc cref="Annotate"/>
    protected abstract IAST _AnnotateInner(STAnnotater ann);

    /// <summary>
    /// Annotate this syntax tree with types and bindings.
    /// The resulting AST may contain <see cref="AST.Failure"/>.
    /// </summary>
    public IAST Annotate(STAnnotater ann) {
        var ast = _AnnotateInner(ann);
        ast.SetDiagnostics(Diagnostics);
        return ast;
    }
    
    /// <summary>
    /// Print a readable description of the entire syntax tree.
    /// </summary>
    public abstract IEnumerable<PrintToken> DebugPrint();

    private static InvokedMethod[]? GetOverloads(ST func, LexicalScope scope) => func switch {
        FnIdent fn => fn.Func,
        Ident id => scope.GlobalRoot.StaticMethodDeclaration(id.Name.ToLower()) is { } decls ?
            decls.Select(d => d.Call(id.Name)).ToArray() :
            null,
        _ => null
    };
    
    /// <summary>
    /// An identifier that may be for a function or variable or type, etc.
    /// </summary>
    /// <param name="Position">Position of the identifier in the source.</param>
    /// <param name="Name">Name of the identifier.</param>
    /// <param name="Generic">Whether this identifier is generic, ie. contains &lt;&gt; or array markings []. A generic identifier cannot be a variable.</param>
    public record Ident(PositionRange Position, string Name, bool Generic) : ST(Position) {
        /// <summary>
        /// The token representing the explicitly-declared type of this identifier.
        /// </summary>
        public Lexer.Token? KnownType { get; }
        
        /// <inheritdoc cref="ST.Ident"/>
        public Ident(Lexer.Token token, Lexer.Token? type = null) : this(token.Position, token.Content,
            token.Type == Lexer.TokenType.TypeIdentifier) {
            this.KnownType = type;
        }

        /// <summary>
        /// Try to reference the variable with name `id`, defined in the scope of `inImport` (if provided)
        ///  else in `scope`.
        /// <br/>If there is no such script function, return null.
        /// </summary>
        public static AST.Reference? TryFindVariable(PositionRange pos, Ident id, ScriptImport? inImport, LexicalScope scope) {
            if ((inImport?.Ef.Scope ?? scope).FindVariable(id.Name) is { } decl)
                return new AST.Reference(pos, scope, inImport, id.Name, decl) { NameOnlyPosition = id.Position };
            return null;
        }

        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var scope = ann.Scope;
            var kt = LangParser.TypeFromToken(KnownType, scope);
            if (!kt.TryL(out var typ))
                return kt.Right;
            if (typ is null && ann.VarReplace != null && ann.VarReplace.TryGetValue(Name, out var repl) && repl != this)
                return repl._AnnotateInner(ann);
            if (Name.StartsWith("&")) {
                //For scriptFunction we can scan upwards for the single declaration; for dynamic reference, we can't,
                // since there are possibly multiple declarations in different places.
                //That said, we make an exception for const vars for convenience.
                var key = Name.Substring(1);
                if (typ is null && scope.FindVariable(key, DeclarationLookup.ConstOnly) is { } decl) {
                    return new AST.Reference(Position, scope, null, key, decl);
                }
                if (scope.IsConstScope)
                    return new AST.Failure(new(Position, "Cannot use dynamic lookup within a constant scope."), scope);
                var ast = new AST.WeakReference(Position, scope, key, typ);
                ast.AddTokens([Type(KnownType?.Position)]);
                return ast;
            }
            if (typ != null)
                return new AST.Failure(new(Position,
                    "Variable type annotation is only allowed when declaring a variable " +
                    "(eg. `var x::float = 5`) or using a dynamically-scoped variable " +
                    "(eg. `&x::float + 1`)."), scope);
            if (TryFindVariable(Position, this, null, scope) is { } tree)
                return tree;
            if (scope.GlobalRoot.StaticMethodDeclaration(Name)?.Where(m => m.Params.Length == 0).ToArray() is { Length: >0 } meths)
                return AST.MethodCall.Make(Position, Position, ann, meths.SelectToArr(m => m.Call(Name)), System.Array.Empty<ST>());
            if (ann.Scope.GlobalRoot.EnumResolver.TryGetValue(Name, out var vals))
                return new AST.Reference(Position, scope, null, Name, vals);

            if (scope.ImportDecls.TryGetValue(Name, out var imp)) {
                return new AST.Failure(new(Position, $"`{Name}` is an imported file. You must reference a variable or function defined within the file; eg. `{Name}.myFloat` or `{Name}.myFunction(1, 2)`."),
                    scope) {
                    ImportedScript = imp
                };
            }
            
            var unreachable = scope.ScriptRoot.AllVarsInDescendantScopes
                .Concat(scope.GlobalRoot.AllVisibleVars)
                .Where(x => x.Name == Name).ToList();
            var err = $"Could not determine what \"{Name}\" refers to.";
            if (unreachable.Count > 0) {
                err +=
                    $"\nThere are some declarations for \"{Name}\", but they are not visible from this lexical scope:" +
                    $"\n\t{string.Join("\n\t", unreachable.Select(x => $"{x.Name} at {x.Position}"))}";
                if (scope.IsConstScope && unreachable.Any(u => !u.Constant)) {
                    err += "\nThis is a constant scope. You can only reference const variables.";
                } else {
                    err += $"\nMaybe you need to hoist the declaration for \"{Name}\" (use `hvar` instead of `var`).";
                    if (scope is DynamicLexicalScope)
                        err +=
                            $"\nAlso, this is a dynamic scope. You may need to use dynamic scoping on the variable (ie. `&{Name}` instead of `{Name}`).";
                }
            }
            return new AST.Failure(new(Position, err), scope);
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "&" + Name;
        }
    }

    /// <summary>
    /// An identifier that is known to be for a static method. This is generally only constructed by operators.
    /// </summary>
    public record FnIdent(PositionRange Position, params InvokedMethod[] Func) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return Func[0].Name;
        }
    }

    /// <summary>
    /// An `import` statement that brings some other script into the namespace.
    /// </summary>
    public record Import(PositionRange KwPos, Lexer.Token File, (PositionRange At, Lexer.Token Filename)? Location, (PositionRange As, Lexer.Token Desc)? Name) :
        ST(KwPos.Merge(Name?.Desc.Position ?? File.Position)) {
        
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var ef = ServiceLocator.Find<ILangCustomizer>().Import(this);
            if (ef.IsRight)
                return new AST.Failure(ef.Right, ann.Scope);
            var decl = new ScriptImport(Position, ef.Left, File.Content, Location?.Filename.Content,
                Name?.Desc.Content);
            AST ast;
            if (ann.Scope.Declare(decl) is {IsRight:true} r)
                ast = new AST.Failure(new(Position, $"The variable `{decl.Name}` has already been declared at {r.Right.Position}."), ann.Scope);
            else {
                ast = new AST.DefaultValue(Position, ann.Scope, typeof(void)) {
                    TokenType = null,
                    Description = () => $"Import {File.Content}" + (Name is { } n ? $" as {n.Desc.Content}" : null)
                };
            }
            ast.AddTokens([
                Keyword(KwPos), Keyword(Location?.At), 
                Token(Location?.Filename.Position, SemanticTokenTypes.String), Keyword(Name?.As)
            ]);
            return (IAST)ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"import {File.Content}" + (Name is {} n ? $" as {n.Desc.Content}" : null);
        }
    }

    /// <summary>
    /// A return statement.
    /// </summary>
    public record Return(PositionRange KwPos, ST? Value) : ST(Value == null ? KwPos : KwPos.Merge(Value.Position)) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            if (ann.Scope.NearestReturn is null)
                return new AST.Failure(
                    new(Position, "This return statement is not contained within a function definition."), ann.Scope);
            var ast = new AST.Return(Position, ann.Scope, Value?.Annotate(ann));
            ast.AddTokens([Keyword(KwPos)]);
            return ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "return";
            if (Value != null)
                foreach (var w in Value.DebugPrint())
                    yield return w;
        }
    }
    
    /// <summary>
    /// A continue statement.
    /// </summary>
    public record Continue(PositionRange Position) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            if (ann.Scope.NearestContinueBreak is null)
                return new AST.Failure(
                    new(Position, "This continue statement is not contained within a loop."), ann.Scope);
            return new AST.Continue(Position, ann.Scope);
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "continue";
        }
    }
    /// <summary>
    /// A break statement.
    /// </summary>
    public record Break(PositionRange Position) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            if (ann.Scope.NearestContinueBreak is null)
                return new AST.Failure(
                    new(Position, "This break statement is not contained within a loop."), ann.Scope);
            return new AST.Break(Position, ann.Scope);
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "continue";
        }
    }

    /// <summary>
    /// A joint declaration-assignment, such as `var x = 5`.
    /// </summary>
    public record VarDeclAssign(Lexer.Token VarKw, Lexer.Token Id, Lexer.Token? Typ, PositionRange EqPos, ST AssignValue) : 
        ST(VarKw.Position.Merge(AssignValue.Position)) {
        /// <inheritdoc cref="ExMAssign.VariableInitialize{T}"/>
        public static readonly MethodSignature VarInitialize =
            LangParser.Meth(typeof(ExMAssign), nameof(ExMAssign.VariableInitialize));
        
        /// <summary>
        /// Location of the `const` keyword before the declaration.
        /// </summary>
        public PositionRange? ConstKwPos { get; set; }
        
        /*private readonly FunctionCall Assignment =
            new(Declaration.Position.Merge(AssignValue.Position),
                new FnIdent(EqPos, VarInitialize.Call(null)),
                new Ident(Declaration.Position, Declaration.Name, false),
                AssignValue);*/
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var kt = LangParser.TypeFromToken(Typ, ann.Scope);
            if (!kt.TryL(out var typ))
                return kt.Right;
            var decl = new VarDecl(Id.Position, VarKw.Content == "hvar", typ, Id.Content);
            if (ConstKwPos != null)
                decl.Constant = true;
            
            if (ann.Scope.Declare(decl) is { IsRight:true} r)
                return new AST.Failure(new(Position, 
                    $"The variable {decl.Name} has already been declared at {r.Right.Position}."), ann.Scope);
            var assignScope = ann.Scope;
            if (decl.Constant) {
                assignScope = LexicalScope.Derive(assignScope);
                assignScope.IsConstScope = true;
                assignScope.Type = LexicalScopeType.ExpressionBlock;
            }
            var ret = new AST.MethodCall(decl.Position.Merge(AssignValue.Position), EqPos, ann.Scope, [VarInitialize.Call(null)
            ], new[] {
                new Ident(decl.Position, decl.Name, false).Annotate(ann),
                AssignValue.Annotate(ann with { Scope = assignScope })
            });
            ret.AddTokens([Keyword(ConstKwPos), Keyword(VarKw.Position), Type(Typ?.Position)]);
            return ret;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"{VarKw.Content} {Id.Content} = ";
            foreach (var w in AssignValue.DebugPrint())
                yield return w;
        }
    }

    private ReflectionException emptyMemberErr => new(Position, "An identifier is required after this period.");

    /// <summary>
    /// A member access `x.y`.
    /// `x` may be an import name, in which case this is an imported script member,
    /// or `x` may be a type name, in which case this is a static member,
    /// or `x` may be a variable, in which case this is an instance member.
    /// </summary>
    public record MemberAccess(ST Object, Ident Member) : ST(Object.Position.Merge(Member.Position)) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            if (Object is Ident id) {
                if (ann.Scope.ImportDecls.TryGetValue(id.Name, out var imp)) {
                    if (Ident.TryFindVariable(Position, Member, imp, ann.Scope) is { } tree) {
                        tree.AddTokens([Keyword(Object.Position)]);
                        return tree;
                    } else {
                        return new AST.Failure(Member.Name.Length > 0 ?
                                new(Position,
                                    $"There is no reference with name `{Member.Name}` in import `{imp.Name}`.") :
                                new(Member.Position,
                                    $"An identifier is required after this period, eg. `{imp.Name}.myVariable`.")
                            , imp.Ef.Scope) { ImportedScript = imp, IsImportedScriptMember = true };
                    }
                } else if (LangParser.TypeFromString(id.Name).TryL(out var typ)) {
                    var methods = typ.GetMember(Member.Name)
                        .SelectNotNull(TypeMember.MaybeMake)
                        .Where(m => m.Static && m.Params.Length == 0)
                        .ToList();
                    if (methods.Count == 0)
                        return new AST.Failure(Member.Name.Length == 0 ? emptyMemberErr : new(Position, 
                                $"No member {Member.Name} was found on type {typ.RName()}"), ann.Scope) 
                            { Completions = (typ, Member.Name) };
                    var ast = AST.MethodCall.Make(Position, Member.Position, ann,
                        methods.SelectToArr(m => MethodSignature.Get(m).Call(Member.Name)), System.Array.Empty<ST>());
                    ast.AddTokens([Type(id.Position)]);
                    return ast;
                }
            }
            var inst = Object.Annotate(ann);
            if (Member.Name.Length == 0)
                return new AST.InstanceFailure(emptyMemberErr, ann.Scope, inst);
            return new AST.InstanceMethodCall(Position, Member.Position, ann.Scope, Member.Name, inst);
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Object.DebugPrint())
                yield return w;
            yield return ".";
            foreach (var w in Member.DebugPrint())
                yield return w;
        }
    }
    /// <summary>
    /// A member function call `x.f(y)`.
    /// `x` may be an import name, in which case this is an imported script function,
    /// or `x` may be a type name, in which case this is a static function,
    /// or `x` may be a variable, in which case this is an instance function.
    /// </summary>
    public record MemberFunction(PositionRange Position, ST Object, Ident Member, ST[] Args) : ST(Position) {

        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var (memName, typArgs) = LangParser.TypeArgsFromString(Member.Name);
            if (Object is Ident id) {
                if (ann.Scope.ImportDecls.TryGetValue(id.Name, out var imp)) {
                    if (FunctionCall.LoadScriptFnDecl(Position, Member, imp, ann, Args) is { } sfn) {
                        (sfn as AST)?.AddTokens([Keyword(Object.Position)]);
                        return sfn;
                    } else if (imp.Ef.Scope.MacroDecls.TryGetValue(Member.Name, out var macro)) {
                        var ast = MacroDef.AnnotateAtCallsite(Position, Member.Position, macro, ann, Args);
                        (ast as AST)?.AddTokens([Keyword(Object.Position)]);
                        return ast;
                    } else {
                        return new AST.Failure(Member.Name.Length > 0 ?
                                new(Position,
                                    $"There is no script function with name `{Member.Name}` in import `{imp.Name}`.") :
                                new(Member.Position,
                                    $"An identifier is required after this period, eg. `{imp.Name}.myFunction`."),
                            imp.Ef.Scope) { ImportedScript = imp, IsImportedScriptMember = true };
                    }
                } else if (LangParser.TypeFromString(id.Name).TryL(out var typ)) {
                    var methods = typ.GetMember(memName)
                        .Where(m => typArgs.Length == 0 || m is MethodInfo { IsGenericMethodDefinition: true } mi 
                                && mi.GetGenericArguments().Length == typArgs.Length)
                        .SelectNotNull(m => {
                            if (typArgs.Length > 0 && m is MethodInfo mi)
                                m = mi.MakeGenericMethod(typArgs);
                            return TypeMember.MaybeMake(m);
                        })
                        .Where(m => m.Static && m.Params.Length == Args.Length)
                        .ToList();
                    if (methods.Count == 0) {
                        return new AST.Failure(Member.Name.Length == 0 ?
                                    emptyMemberErr :
                                    new(Position,
                                        $"No method {Member.Name} with {Args.Length} arguments was found on type {typ.RName()}"),
                                ann.Scope)
                            { Completions = (typ, Member.Name) };
                    }
                    var ast = AST.MethodCall.Make(Position, Member.Position, ann,
                        methods.SelectToArr(m => MethodSignature.Get(m).Call(Member.Name)), Args);
                    ast.AddTokens([Type(id.Position)]);
                    return ast;
                }
            }
            var inst = Object.Annotate(ann);
            if (Member.Name.Length == 0)
                return new AST.InstanceFailure(emptyMemberErr, ann.Scope, inst);
            return new AST.InstanceMethodCall(Position, Member.Position, ann.Scope, memName,
                Args.Select(a => a.Annotate(ann)).Prepend(inst).ToArray()) {
                GenericTypes = typArgs
            };
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Object.DebugPrint())
                yield return w;
            yield return ".";
            foreach (var w in Member.DebugPrint())
                yield return w;
        }
    }

    /// <inheritdoc cref="AST.Indexer"/>
    public record Indexer(ST Object, PositionRange OpenBrace, ST Index, PositionRange CloseBrace)
        : ST(Object.Position.Merge(CloseBrace)) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var ast = new AST.Indexer(Position, ann.Scope, Object.Annotate(ann), Index.Annotate(ann));
            ast.AddTokens([Op(OpenBrace), Op(CloseBrace)]);
            return ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Object.DebugPrint())
                yield return w;
            yield return "[";
            foreach (var w in Index.DebugPrint())
                yield return w;
            yield return "]";
        }
    }

    /// <summary>
    /// A C#-style function call with parentheses and commas, such as `f(x, y)`,
    ///  or an operator-based function call such as `x + y`.
    /// <br/>Note that there must be no spaces before the parentheses.
    /// </summary>
    public record FunctionCall(PositionRange Position, ST Fn, params ST[] Args) : ST(Position) {
        /// <inheritdoc cref="IMethodTypeTree{T}.OverloadsAreInterchangeable"/>
        public bool OverloadsInterchangeable { get; init; } = false;

        /// <summary>
        /// Try to invoke the script function with name `id`, defined in the ann.Scope of `inImport` (if provided)
        ///  else in `scope`.
        /// <br/>If there is no such script function, return null.
        /// </summary>
        public static IAST? LoadScriptFnDecl(PositionRange Position, Ident id, ScriptImport? inImport, STAnnotater ann, IReadOnlyList<ST> Args) {
            var fnScope = inImport?.Ef.Scope ?? ann.Scope;
            var (key, isDynamic) = (id.Name[0] == '&' ? (id.Name.Substring(1), inImport == null) : (id.Name, false));
            var sfn = fnScope.FindScopedFunction(key, isDynamic ? DeclarationLookup.Dynamic : DeclarationLookup.Standard);
            if (sfn != null) {
                var deflts = new List<IAST>();
                for (int ii = Args.Count; ii < sfn.CallType.Arguments.Length - 1; ++ii) {
                    if (sfn.Defaults[ii] is not null) {
                        deflts.Add(new AST.DefaultValue(Position, ann.Scope, AsFunctionArg: true) { TokenType = null });
                    } else {
                        return new AST.Failure(new(Position, id.Position,
                            $"The script function `{id.Name}` takes {sfn.CallType.Arguments.Length - 1} arguments," +
                            $" but {Args.Count} were provided."), fnScope);
                    }
                }
                return new AST.ScriptFunctionCall(Position, id.Position, ann.Scope, inImport, sfn, isDynamic,
                    Args.Select(a => a.Annotate(ann)).Concat(deflts).ToArray());
            } else return null;
        }

        /// <summary>
        /// Create an error string for the method by the given name not being found.
        /// </summary>
        public static string NoMethodFoundErr(string name, LexicalScope scope) {
            var err = $"Couldn't find any method by the name `{name}`.";
            var unreachable = scope.ScriptRoot.AllFnsInDescendantScopes.Where(x => x.Name == name).ToList();
            if (unreachable.Count > 0) {
                err += $"\nThere are some declarations for \"{name}\", but they are not visible from this lexical scope:" + 
                       $"\n\t{string.Join("\n\t", unreachable.Select(x => $"{x.Name} at {x.Position}"))}";
                if (scope.IsConstScope && unreachable.Any(u => !u.IsConstant)) 
                    err += "\nThis is a constant scope. You can only reference const functions.";
                else {
                        err += $"\nMaybe you need to hoist the declaration for \"{name}\" (use `hfunction` instead of `function`).";
                    if (scope is DynamicLexicalScope)
                        err += $"\nThis is a dynamic scope. You may need to use dynamic scoping on the function " +
                           $"(ie. `&{name}(args)` instead of `{name}(args)`).";
                }
            }
            return err;
        }
        
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            //If we're directly calling a *static method*, then we already know the signatures
            if (Fn is FnIdent fn) {
                return AST.MethodCall.Make(Position, fn.Position, ann, fn.Func, Args, OverloadsInterchangeable);
            } else if (Fn is Ident id) {
                var (name, targs) = LangParser.TypeArgsFromString(id.Name);
                if (LoadScriptFnDecl(Position, id, null, ann, Args) is {} sfn) {
                    return sfn;
                } else if (ann.Scope.GlobalRoot.StaticMethodDeclaration(name.ToLower()) is { } decls) {
                    var argFilter = decls.SelectNotNull(d => {
                        if (d.Params.Length != Args.Length) return null;
                        if (targs.Length == 0) return d;
                        if (d is not GenericMethodSignature gm || gm.TypeParams != targs.Length) return null;
                        return gm.Specialize(targs);
                    }).Select(d => d.Call(name)).ToArray();
                    if (argFilter.Length > 0)
                        return AST.MethodCall.Make(Position, id.Position, ann, argFilter, 
                            Args, OverloadsInterchangeable);
                    else {
                        var prms = ann.AnnotateAll(Args);
                        if (prms.Length == 0) {
                            var nextPos = new PositionRange(id.Position.End, Position.End);
                            prms = [
                                new AST.Failure(new(nextPos, "At least one argument is required here."), ann.Scope) {
                                    PossibleTypes = decls.Select(x => x.SharedType.Arguments[0]).Distinct().ToArray()
                                }
                            ];
                        }
                        return new AST.Failure(new(Position, id.Position,
                            $"There is no method by name `{id.Name}` that takes {Args.Length} arguments." +
                            $"\nThe signatures of the methods named `{id.Name}` are as follows:" +
                            $"\n\t{string.Join("\n\t", decls.Select(o => o.AsSignature))}"), ann.Scope, prms) {
                            Completions = decls
                        };
                    }
                } else if (ann.Scope.FindVariable(id.Name) is { } varn) {
                    //Note that we must prioritize static methods over lambdas in the basic case due to 
                    // "times" being a GCX property method and an autovar!
                    return new AST.LambdaCall(Position, ann.Scope, Args.Select(a => a.Annotate(ann)).Prepend(
                        new AST.Reference(id.Position, ann.Scope, null, id.Name, varn)).ToArray());
                } else if (ann.Scope.MacroDecls.TryGetValue(id.Name, out var macro)) {
                    return MacroDef.AnnotateAtCallsite(Position, Fn.Position, macro, ann, Args);
                } else {
                    return new AST.Failure(new(Position, NoMethodFoundErr(id.Name, ann.Scope)), ann.Scope);
                }
            } else if (Fn is not DefaultValue)
                return new AST.LambdaCall(Position, ann.Scope, Args.Prepend(Fn).Select(a => a.Annotate(ann)).ToArray());
            else
                return new AST.Failure(new(Position, "Cannot call `null`."), ann.Scope);
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Fn.DebugPrint())
                yield return w;
            yield return "(";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return ")";
        }
    }

    /// <summary>
    /// A `new Type(args)` method call.
    /// </summary>
    public record Constructor(PositionRange Position, PositionRange NewKw, Lexer.Token Typ, params ST[] Args) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var kt = LangParser.TypeFromToken(Typ, ann.Scope);
            if (!kt.TryL(out var typ))
                return kt.Right;
            var decls = typ!.GetConstructors().SelectNotNull(MethodSignature.MaybeGet).ToList();
            var argFilter = decls.Where(d => d.Params.Length == Args.Length).ToList();
            if (argFilter.Count > 0) {
                var ast = AST.MethodCall.Make(Position, Typ.Position, ann, argFilter.SelectToArr(d => d.Call(null)), Args);
                ast.AddMethodSemanticToken = false;
                ast.AddTokens([Keyword(NewKw), Type(Typ.Position)]);
                return ast;
            } else {
                var prms = ann.AnnotateAll(Args);
                return new AST.Failure(new(Position, Typ.Position,
                    $"There is no constructor for type {typ.RName()} that takes {Args.Length} arguments." +
                    $"\nThe valid constructors are as follows:" +
                    $"\n\t{string.Join("\n\t", decls.Select(o => o.AsSignature))}"), ann.Scope, prms) {
                    Completions = decls
                };
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"new {Typ.Content}(";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return ")";
            
        }
    }
    
    /// <summary>
    /// A function call that explicitly declares possibly fewer arguments than required by the function.
    /// <br/>In the form $(f, a1, a2, a3); where in this case function f requires 3 or more arguments.
    /// </summary>
    public record PartialFunctionCall(PositionRange Position, ST Func, params ST[] Args) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            if (Func is Ident Fn) {
                if (ann.Scope.GlobalRoot.StaticMethodDeclaration(Fn.Name.ToLower()) is { } decls) {
                    var argFilter = decls.Where(d => d.Params.Length >= Args.Length).ToList();
                    if (argFilter.Count > 0)
                        return new AST.PartialMethodCall(Position, Fn.Position, ann.Scope,
                            argFilter.SelectToArr(a => a.Call(Fn.Name)),
                            ann.AnnotateAll(Args));
                    else
                        return new AST.Failure(new(Position, Fn.Position,
                            $"There is no method by name `{Fn.Name}` that takes at least {Args.Length} arguments." +
                            $"\nThe signatures of the methods named `{Fn.Name}` are as follows:" +
                            $"\n\t{string.Join("\n\t", decls.Select(o => o.AsSignature))}"), ann.Scope) {
                            Completions = decls
                        };
                } else if (ann.Scope.FindVariable(Fn.Name) is { } varn) {
                    return new AST.PartialLambdaCall(Position, ann.Scope, Args.Select(a => a.Annotate(ann)).Prepend(
                        new AST.Reference(Fn.Position, ann.Scope, null, Fn.Name, varn)).ToArray());
                } else if (ann.Scope.FindScopedFunction(Fn.Name, DeclarationLookup.Standard) is {} sfn)
                    return new AST.PartialScriptFunctionCall(Position, Fn.Position, ann.Scope, null, sfn, ann.AnnotateAll(Args));
                else
                    return new AST.Failure(new(Position, FunctionCall.NoMethodFoundErr(Fn.Name, ann.Scope)), ann.Scope)
                        {Completions = null as List<MethodSignature> };
            } else
                return new AST.PartialLambdaCall(Position, ann.Scope, 
                    Args.Prepend(Func).Select(a => a.Annotate(ann)).ToArray());
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Func.DebugPrint())
                yield return w;
            yield return "(";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return ")";
        }
    }

    
    /// <summary>
    /// A Haskell-style curried function call, such as `f x`.
    /// <br/>In cases where multiple arguments are applied, this is constructed left-associatively as
    ///  `f x y z` = P(P(P(f, x), y), z).
    /// </summary>
    public record CurriedFunctionCall(ST Fn, ST Arg) : ST(Fn.Position.Merge(Arg.Position)) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            //Get the leftmost function and a list of args
            var argsl = new List<ST>() { Arg };
            var leftmost = Fn;
            while (leftmost is CurriedFunctionCall pfc) {
                argsl.Add(pfc.Arg);
                leftmost = pfc.Fn;
            }
            argsl.Reverse();
            var args = argsl.ToArray();

            (string name, IEnumerable<(int reqArgs, IEnumerable<InvokedMethod> methods)>)? ArgCounts(ST func) {
                if (func switch {
                        FnIdent fn => fn.Func,
                        Ident id => 
                            Ident.TryFindVariable(id.Position, id, null, ann.Scope) is null &&
                                ann.Scope.GlobalRoot.StaticMethodDeclaration(id.Name.ToLower()) is { } decls ?
                                    decls.SelectToArr(d => d.Call(id.Name)) :
                                    null,
                        _ => null
                    } is not { } overloads) {
                    return null;
                }
                return (overloads[0].CalledAs ?? overloads[0].Name,
                    overloads.GroupBy(o => o.Params.Length).Select(g => (g.Key, g as IEnumerable<InvokedMethod>)));
            }
            
            IEnumerable<(ImmutableList<(int index, int argCt)> pArgs, int endsAt)>
                PossibleArgCounts(int index, ImmutableList<(int index, int argCt)> preceding) {
                if (index >= args.Length)
                    return System.Array.Empty<(ImmutableList<(int, int)>, int)>();
                var item = index >= 0 ? args[index] : leftmost;
                if (ArgCounts(item) is not { } counts)
                    //This is not a function, so we consume zero args, but increment the index
                    // since this object itself takes up a space
                    return [(preceding.Add((index, 0)), index + 1)];
                else 
                    //Note that we may want to prepend `preceding.Add((index, 0)), index + 1)` to this,
                    // which is the case of using a function name as a lambda
                    return counts.Item2
                        .Where(consumed => index + 1 + consumed.reqArgs <= args.Length)
                        .SelectMany(consumed => {
                            IEnumerable<(ImmutableList<(int, int)>, int)> cac = [(preceding.Add((index, consumed.reqArgs)), index + 1)
                            ];
                            for (int ii = 0; ii < consumed.reqArgs; ++ii) {
                                cac = cac.SelectMany(ca => PossibleArgCounts(ca.Item2, ca.Item1));
                            }
                            return cac;
                        });
            }
            string? ArgCountErrForIndex(int ii) {
                if (ArgCounts(args[ii]) is not { } cts) {
                    return null;
                    //return $"Argument #{ii + 1}: Not a static function, " +
                    //       $"cannot be partially applied (at {args[ii].Position})";
                } else
                    return $"Argument #{ii + 1}: Function `{cts.name}` with possible parameter counts: " +
                           $"{string.Join(", ", cts.Item2.Select(i => i.reqArgs))} (at {args[ii].Position})";
            }
            if (GetOverloads(leftmost, ann.Scope) is not { } overloads) {
                return new AST.Failure(new(leftmost.Position,
                    "The parser thinks this is a method call, but no static method with this name was found. " +
                    "If this is an instance method, script function, or lambda, please parenthesize the arguments. " +
                    "If this is not a method call, then there is something wrong with your syntax."), ann.Scope);
            }
            //Do a quick check to see if the function grouping makes sense in terms of types
            // This is a weak check, so it may return True if the grouping is actually unsound
            bool GroupingIsTypeFeasible(ImmutableList<(int index, int argCt)> grouping) {
                (TypeTree.ITree tree, int nextIndex) MakeForIndex(int index) {
                    //Don't bother type-enforcement on non-function idents
                    if (ArgCounts(index >= 0 ? args[index] : leftmost) is not { } cts)
                        return (new TypeTree.AtomicWithType(new TypeDesignation.Variable()), index + 1);
                    var ct = grouping[index + 1].argCt;
                    var overloads = cts.Item2
                        .First(x => x.reqArgs == ct).methods
                        .Select(x => x.Method);
                    var nextIndex = index + 1;
                    var prms = new TypeTree.ITree[ct];
                    for (int ii = 0; ii < ct; ++ii) {
                        (prms[ii], nextIndex) = MakeForIndex(nextIndex);
                    }
                    return (new TypeTree.Method(overloads.ToArray(), prms), nextIndex);
                }
                var (tree, last) = MakeForIndex(-1);
                if (last != args.Length)
                    return false;
                return tree.PossibleUnifiers(ann.Scope.GlobalRoot.Resolver, Unifier.Empty).IsLeft;
            }
            //If there are overloads that match the numerical requirements, use them
            var sameArgsReq = overloads.Where(f => f.Params.Length == args.Length).ToArray();
            if (sameArgsReq.Length > 0)
                return AST.MethodCall.Make(Position, leftmost.Position, ann, sameArgsReq, args);
            //If there are overloads that require fewer args than provided, use them and do smart grouping
            var lessArgsReq = overloads.Where(f => f.Params.Length < args.Length).ToArray();
            if (lessArgsReq.Length > 0) {
                var possibleArgGroupings = PossibleArgCounts(-1, ImmutableList<(int, int)>.Empty)
                    .Where(cac => cac.endsAt == args.Length)
                    .ToList();
                bool typeFiltered = false;
                if (possibleArgGroupings.Count > 1) {
                    //Only filter by type if we are required to
                    possibleArgGroupings = possibleArgGroupings
                        .Where(p => GroupingIsTypeFeasible(p.pArgs)).ToList();
                    typeFiltered = true;
                }
                if (possibleArgGroupings.Count != 1) {
                    var plural = possibleArgGroupings.Count == 0 ? "no combination" : "multiple combinations";
                    var argsErr = string.Join("\n\t", args.Length.Range().SelectNotNull(ArgCountErrForIndex));
                    if (typeFiltered && possibleArgGroupings.Count == 0) {
                        return new AST.Failure(new(leftmost.Position,
                            $"When resolving the partial method invocation for `{overloads[0].CalledAs ?? overloads[0].Name}`," +
                            $" {argsl.Count} parameters were provided, but overloads were only found with " +
                            $"{string.Join(", ", overloads.Select(o => o.Params.Length).Distinct().OrderBy(x => x))} parameters." +
                            $"\nAttempted to automatically group functions, but multiple combinations of the following functions " +
                            $"combined to {argsl.Count} parameters, and none of them appear to pass type-checking." +
                            $" Please try using parentheses.\n\t{argsErr}"), ann.Scope);
                    }
                    return new AST.Failure(new(leftmost.Position,
                        $"When resolving the partial method invocation for `{overloads[0].CalledAs ?? overloads[0].Name}`," +
                        $" {argsl.Count} parameters were provided, but overloads were only found with " +
                        $"{string.Join(", ", overloads.Select(o => o.Params.Length).Distinct().OrderBy(x => x))} parameters." +
                        $"\nAttempted to automatically group functions, but {plural} of the following functions " +
                        $"combined to {argsl.Count} parameters. Please try using parentheses.\n\t{argsErr}"), ann.Scope);
                }
                var argsCts = possibleArgGroupings[0].pArgs.ToDictionary(kv => kv.index, kv => kv.argCt);
                var argStack = new Stack<ST>();
                var mArgsTmp = new List<ST>();
                for (int ii = args.Length - 1; ii >= -1; --ii) {
                    var st = ii >= 0 ? args[ii] : leftmost;
                    if (!argsCts.TryGetValue(ii, out var mArgCt) || mArgCt == 0)
                        argStack.Push(st);
                    else {
                        for (int ima = 0; ima < mArgCt; ++ima)
                            mArgsTmp.Add(argStack.Pop());
                        var mOverloads = (GetOverloads(st, ann.Scope) ??
                                          throw new StaticException("Incorrect partial application parameter grouping"))
                            .Where(o => o.Params.Length == mArgCt)
                            .ToArray();
                        if (mOverloads.Length == 0)
                            throw new StaticException("Incorrect partial application overload lookup");
                        argStack.Push(
                            new FunctionCall(st.Position.Merge(mArgsTmp[^1].Position), st, mArgsTmp.ToArray()));
                        mArgsTmp.Clear();
                    }
                }
                return argStack.Pop().Annotate(ann);
            } else
                return new AST.Failure(new(leftmost.Position,
                    $"When resolving the partial method invocation for `{overloads[0].CalledAs ?? overloads[0].Name}`," +
                    $" {argsl.Count} parameters were provided, but overloads were only found with " +
                    $"{string.Join(", ", overloads.Select(o => o.Params.Length).Distinct().OrderBy(x => x))} parameters."),
                    ann.Scope
                );
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Fn.DebugPrint())
                yield return w;
            yield return "(|";
            foreach (var w in Arg.DebugPrint())
                yield return w;
            yield return "|)";
        }
    }

    /// <inheritdoc cref="AST.TypeAs"/>
    public record TypeAs(ST Object, PositionRange AsKw, ST TypeDef) : ST(Object.Position.Merge(TypeDef.Position)) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            if (TypeDef is not Ident tid)
                return new AST.Failure(new(TypeDef.Position, "This must be a type name."), ann.Scope);
            var ptyp = LangParser.TypeFromString(tid.Name);
            if (!ptyp.TryL(out var typ))
                return new AST.Failure(new(TypeDef.Position, ptyp.Right), ann.Scope) { IsTypeCompletion = true };
            var ast = new AST.TypeAs(Position, ann.Scope, typ, Object.Annotate(ann));
            ast.AddTokens([Keyword(AsKw), Type(TypeDef.Position)]);
            return ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Object.DebugPrint())
                yield return w;
            yield return " as ";
            foreach (var w in TypeDef.DebugPrint())
                yield return w;
        }
    }

    /// <summary>
    /// Definition of a macro.
    /// </summary>
    public record MacroDef(Lexer.Token Kw, Lexer.Token Name, List<(Lexer.Token name, (Lexer.Token eqKw, ST defltVal)? deflt)> Args, Block Body) : ST(Kw.Position.Merge(Body.Position)) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            if (Body.Args.Count != 1)
                return new AST.Failure(new(Position, "A macro definition must have a body of exactly one line."),
                    ann.Scope);
            var decl = new MacroDecl(this, Name.Content, Args.SelectToArr(a => a.name.Content), 
                                                         Args.SelectToArr(a => a.deflt?.defltVal));
            if (ann.Scope.Declare(decl).TryR(out var prev))
                return new AST.Failure(new(Position, 
                    $"The macro {decl.Name} has already been declared at {prev.Position}."), ann.Scope);
            var ast = new AST.DefaultValue(Position, ann.Scope, typeof(void)) {
                TokenType = null,
                //doc comment is not filled out until after annotation, so we use Func<string>
                Description = () => $"Macro definition {Name.Content}{decl.DocCommentDisplay()}"
            };
            ast.AddTokens(new[]
                { Keyword(Kw.Position), new SemanticToken(Name.Position, SemanticTokenTypes.Function) }
                .Concat(Args.Select(a => Op(a.deflt?.eqKw.Position))));
            return ast;
        }

        /// <summary>
        /// Expand a macro at a target location in the source code.
        /// </summary>
        public static IAST AnnotateAtCallsite(PositionRange pos, PositionRange meth, MacroDecl decl, STAnnotater ann, IReadOnlyList<ST> args) {
            if (args.Count > decl.Args.Length)
                return new AST.Failure(new(pos, $"Too many arguments provided to macro {decl.Name}"),
                    ann.Scope);
                
            var repl = ann.VarReplace is null ? new() : new Dictionary<string, ST>(ann.VarReplace);
            for (int ii = 0; ii < decl.Args.Length; ++ii) {
                ST target;
                if (ii >= args.Count || args[ii] is DefaultValue { asFunctionArg: true }) {
                    if (decl.Defaults[ii] is { } dflt)
                        target = dflt;
                    else
                        return new AST.Failure(new(pos, $"No default value was provided for argument " +
                                                         $"#{ii + 1} of macro {decl.Name}"), ann.Scope);
                } else
                    target = args[ii];
                repl[decl.Args[ii]] = target;
            }
            //a macro has exactly one line. we want that line, not the body as a block.
            var ast = decl.Tree.Body.Args[0].Annotate(ann with { VarReplace = repl });
            return new AST.InvokedMacro(pos, meth, decl, ast);
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "macro";
            yield return Name.Content;
            yield return "(";
            foreach (var (a, _) in Args)
                yield return a.Content;
            yield return ") {";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in Body.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
            yield return "}";
        }
    }
    
    /// <inheritdoc cref="AST.ScriptFunctionDef"/>
    public record FunctionDef(Lexer.Token Kw, Lexer.Token Name, List<((Lexer.Token name, Lexer.Token? typ), (Lexer.Token eqKw, ST defltVal)? deflt)> Args,
        Lexer.Token? ReturnType, Block Body) : ST(Kw.Position.Merge(Body.Position)) {
        /// <summary>
        /// Position of the `const` keyword for this function def, if it exists.
        /// </summary>
        public PositionRange? ConstKwPos { get; set; }
        
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            bool hoist = Kw.Content == "hfunction";
            var localScope = LexicalScope.Derive(hoist ? ann.Scope.HoistedScope : ann.Scope);
            //There isn't a special scope type for functions, but they're basically standard scopes.
            //Even though they are ultimately compiled to expressions, the context in which they are run
            // is closest to standard scopes, and not comparable to the delayed execution of GCXF (ExpressionEF type).
            localScope.Type = LexicalScopeType.Standard;
            localScope.IsConstScope = ConstKwPos != null;
            var krt = LangParser.TypeFromToken(ReturnType, ann.Scope, allowVoid:true);
            if (!krt.TryL(out var retTyp))
                return krt.Right;
            var rt = localScope.Return = new ReturnStatementConfig(localScope, retTyp);
            var args = new ImplicitArgDecl[Args.Count];
            var deflts = new IAST?[Args.Count];
            for (int ii = 0; ii < Args.Count; ++ii) {
                var ((name, mtypStr), deflt) = Args[ii];
                var kat = LangParser.TypeFromToken(mtypStr, ann.Scope);
                if (!kat.TryL(out var argTyp))
                    return kat.Right;
                args[ii] = new ImplicitArgDecl(name.Position, argTyp, name.Content);
                if (deflt.HasValue) {
                    deflts[ii] = deflt.Value.defltVal.Annotate(ann); //enclosing scope, not local scope
                }
            }
            var fnCallType =
                TypeDesignation.Dummy.Method(rt.Type, args.SelectToArr(a => a.TypeDesignation));
            var decl = new ScriptFnDecl(null!, hoist, Name.Content, args, deflts, fnCallType) {
                IsConstant = localScope.IsConstScope
            };
            rt.Function = decl;
            if (ann.Scope.Declare(decl).TryR(out var prev))
                return new AST.Failure(new(Position, 
                    $"The function {decl.Name} has already been declared at {prev.Position}."), ann.Scope);
            //due to return statements, the block content itself may have any type
            var _block = Body.AnnotateWithParameters(ann with { Scope = localScope }, args);
            if (!_block.TryL(out var block))
                return _block.Right;
            var ast = new AST.ScriptFunctionDef(Position, Name.Content, ann.Scope, localScope, decl, block);
            decl.Tree = ast;
            ast.AddTokens(Args.Select(a => Type(a.Item1.typ?.Position))
                .Concat(Args.Select(a => Op(a.deflt?.eqKw.Position)))
                .Concat([
                    Keyword(ConstKwPos),
                    Keyword(Kw.Position),
                    new SemanticToken(Name.Position, SemanticTokenTypes.Function).WithConst(decl.IsConstant),
                    Type(ReturnType?.Position)
                ]));
            return ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "function";
            yield return Name.Content;
            yield return "(";
            foreach (var ((a, _), _) in Args)
                yield return a.Content;
            yield return ") {";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in Body.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
            yield return "}";
        }
    }

    /// <summary>
    /// A block of statements.
    /// </summary>
    public record Block(PositionRange Position, IReadOnlyList<ST> Args) : ST(Position) {
        /// <inheritdoc cref="AST.Block"/>
        public Block(IReadOnlyList<ST> args) : this(args.Count > 0 ? 
            args[0].Position.Merge(args[^1].Position) : 
            new Position(0, 1, 0).EmptyRange(), args) { }

        private IAST[] AnnotateStmts(STAnnotater ann) {
            if (Args.Count == 0)
                return [new AST.DefaultValue(Position, ann.Scope, typeof(void))];
            var annotated = new IAST[Args.Count];
            for (int ii = 0; ii < Args.Count; ++ii)
                annotated[ii] = Args[ii].Annotate(ann);
            return annotated;
        }
        
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var localScope = LexicalScope.Derive(ann.Scope);
            return new AST.Block(Position, ann.Scope, localScope, AnnotateStmts(ann with {Scope = localScope}));
        }

        //note: these functions requires localScope to be passed in instead of enclosingScope
        /// <summary>
        /// Annotate this block as a top-level block with script arguments.
        /// </summary>
        public Either<AST.Block, AST.Failure> AnnotateWithParameters(STAnnotater ann, IDelegateArg[] arguments) {
            var decls = new ImplicitArgDecl[arguments.Length];
            for (int ii = 0; ii < arguments.Length; ++ii) {
                decls[ii] = arguments[ii].MakeImplicitArgDecl();
            }
            return AnnotateWithParameters(ann, decls);
        }
        
        /// <summary>
        /// Annotate this block as a top-level or function body block with implicit arguments.
        /// </summary>
        public Either<AST.Block, AST.Failure> AnnotateWithParameters(STAnnotater ann, ImplicitArgDecl[] arguments) {
            var localScope = ann.Scope;
            var decls = new (VarDecl, ImplicitArgDecl)[arguments.Length];
            for (int ii = 0; ii < arguments.Length; ++ii) {
                var imp = arguments[ii];
                decls[ii] = (new VarDecl(imp.Position, false, imp.KnownType, imp.Name, imp), imp);
                if (localScope.Declare(decls[ii].Item1) is {IsRight: true} r) {
                    return new AST.Failure(new(Position, 
                        $"The variable {arguments[ii].Name} has already been declared at {r.Right.Position}."), localScope);
                }
            }
            return new AST.Block(Position, localScope.Parent!, localScope, AnnotateStmts(ann))
                .WithFunctionParams(decls);
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"block({{";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return "})";
        }
    }
    
    
    
    /// <summary>
    /// An if expression (x ? y : z) that returns one of two values.
    /// </summary>
    public record IfExpression(ST Condition, ST TrueBody, ST FalseBody)
        : ST(Condition.Position.Merge(FalseBody.Position)) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var ast = new AST.Conditional(Position, ann.Scope, true, Condition.Annotate(ann), TrueBody.Annotate(ann),
                FalseBody.Annotate(ann));
            return ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "(";
            foreach (var w in Condition.DebugPrint())
                yield return w;
            yield return ") ?";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in TrueBody.DebugPrint())
                yield return w;
            yield return ":";
            yield return PrintToken.newline;
            foreach (var w in TrueBody.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
        }
    }

    
    /// <summary>
    /// An if statement (if (x) { y; } else { z; }) with an optional else.
    /// </summary>
    public record IfStatement(PositionRange ifKw, PositionRange? elseKw, ST Condition, Block TrueBody, ST? FalseBody)
        : ST(ifKw.Merge(FalseBody?.Position ?? TrueBody.Position)) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var ast = new AST.Conditional(Position, ann.Scope, false, Condition.Annotate(ann), TrueBody.Annotate(ann),
                FalseBody?.Annotate(ann));
            ast.AddTokens([Keyword(ifKw), elseKw is { } ekp ? Keyword(ekp) : null]);
            return ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "if (";
            foreach (var w in Condition.DebugPrint())
                yield return w;
            yield return ") {";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in TrueBody.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
            yield return "}";
            if (FalseBody != null) {
                yield return " else {";
                yield return PrintToken.indent;
                yield return PrintToken.newline;
                foreach (var w in TrueBody.DebugPrint())
                    yield return w;
                yield return PrintToken.dedent;
                yield return PrintToken.newline;
                yield return "}";
            }
        }
    }

    
    
    /// <summary>
    /// An for or while loop.
    /// </summary>
    public record Loop(PositionRange KwPos, ST? Initializer, ST? Condition, ST? Finalizer, Block Body)
        : ST(KwPos.Merge(Body.Position)) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var localScope = LexicalScope.Derive(ann.Scope);
            localScope.ContinueBreak = (Ex.Label(), Ex.Label(), localScope);
            //order is important for declarations to be read properly
            var localAnn = ann with { Scope = localScope };
            var init = Initializer?.Annotate(localAnn);
            var cond = Condition?.Annotate(localAnn);
            var body = (AST.Block)Body.Annotate(localAnn);
            body.EndWithLabel = localScope.ContinueBreak.Value.c;
            var ast = new AST.Loop(Position, ann.Scope, localScope, init, cond, Finalizer?.Annotate(localAnn), body);
            ast.AddTokens([Keyword(KwPos)]);
            return ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"for (";
            if (Initializer != null)
                foreach (var w in Initializer.DebugPrint())
                    yield return w;
            yield return "; ";
            if (Condition != null)
                foreach (var w in Condition.DebugPrint())
                    yield return w;
            yield return "; ";
            if (Finalizer != null)
                foreach (var w in Finalizer.DebugPrint())
                    yield return w;
            yield return ") {";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in Body.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
            yield return "}";
        }
    }
    
    /// <summary>
    /// An array of items with the same type.
    /// </summary>
    public record Array(PositionRange Position, Lexer.Token? Typ, ST[] Args) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var kt = LangParser.TypeFromToken(Typ, ann.Scope);
            if (!kt.TryL(out var typ))
                return kt.Right;
            if (typ != null) {
                if (!typ.IsArray)
                    return new AST.Failure(new ReflectionException(Typ!.Value.Position, 
                        $"Type {Typ.Value.Content} is not an array type"), ann.Scope);
                typ = typ.GetElementType();
            }
            var ast = new AST.Array(Position, ann.Scope, typ, ann.AnnotateAll(Args));
            ast.AddTokens([Type(Typ?.Position)]);
            return ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"[{Args.Length}] {{";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return "}";
        }
    }
    

    /// <summary>
    /// A number such as `5.0`. This is not included in TypedValue since numbers can have their type
    ///  auto-determined (eg. float vs int) based on usage.
    /// </summary>
    public record Number(PositionRange Position, string Value) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) => new AST.Number(Position, ann.Scope, Value);

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return Value;
        }
    }

    /// <summary>
    /// A keyword returning either the default value of an optional function argument or the default value of a type.
    /// </summary>
    public record DefaultValue(PositionRange Position, Lexer.Token? Typ, bool asFunctionArg = false) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) {
            var kt = LangParser.TypeFromToken(Typ, ann.Scope);
            if (!kt.TryL(out var typ))
                return kt.Right;
            var ast = new AST.DefaultValue(Position, ann.Scope, typ, asFunctionArg);
            ast.AddTokens([Type(Typ?.Position)]);
            return ast;
        }

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return asFunctionArg ? "default" : "null";
        }
    }
    
    /// <summary>
    /// A fixed value of a type not subject to type auto-determination, such as strings, but not numbers.
    /// </summary>
    public record TypedValue<T>(PositionRange Position, T Value) : ST(Position) {
        /// <summary>
        /// The symbol type of this value.
        /// </summary>
        public SymbolKind Kind { get; init; } = SymbolKind.Constant;
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) => 
            new AST.TypedValue<T>(Position, ann.Scope, Value, Kind);
        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return Value?.ToString() ?? "<null>";
        }
    }

    /// <summary>
    /// A tuple.
    /// </summary>
    public record Tuple(PositionRange Position, List<ST> Args) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) =>
            new AST.Tuple(Position, ann.Scope, Args.SelectToArr(a => a.Annotate(ann)));

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "(";
            for (int ii = 0; ii < Args.Count; ++ii) {
                if (ii > 0) 
                    yield return ", ";
                foreach (var x in Args[ii].DebugPrint())
                    yield return x;
            }
            yield return ")";
        }
    }

    /// <summary>
    /// A failed syntax tree parse.
    /// </summary>
    public record Failure(PositionRange Position, string Error) : ST(Position) {
        /// <inheritdoc/>
        protected override IAST _AnnotateInner(STAnnotater ann) =>
            new AST.Failure(new ReflectionException(Position, Error), ann.Scope);

        /// <inheritdoc/>
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"throw new Exception(\"{Error}\")";
        }
    }

}
}