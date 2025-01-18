using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using JetBrains.Annotations;
using Scriptor.Analysis;
using Scriptor.Compile;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor.Expressions;

/// <summary>
/// An arbitrary set of arguments to an expression function.
/// <br/>Expression functions are written in the general form Func&lt;TExArgCtx, TEx&lt;R&gt;&gt;
///  and compiled to Func&lt;T1, T2..., R&gt;, where T1,T2... are types that have stored their information in TExArgCtx,
///  and R is some standard return type like float or Vector2.
/// </summary>
public class TExArgCtx {
    /// <summary>
    /// Context that is shared by any copies of <see cref="TExArgCtx"/>.
    /// </summary>
    public class RootCtx {
        /// <summary>
        /// All active lexical scopes through which this expression context is being passed.
        /// </summary>
        public Stack<LexicalScope> Scope { get; } = new();
        
        /// <summary>
        /// Local variable aliases.
        /// </summary>
        public Dictionary<string, Stack<Ex>> AliasStack { get; } =
            new();
        internal readonly Dictionary<(string, Type), (ParameterExpression, ParameterExpression, ParameterExpression)>
            UnscopedEnvframeAcess = new();

        private static uint nextCtxIndex = 0;
        private uint CtxIndex { get; } = nextCtxIndex++;
        private uint suffixNum = 0;
        
        /// <summary>
        /// Get a unique name for a variable declared in this compilation scope.
        /// </summary>
        public string NameWithSuffix(string s) => $"{s}CG{CtxIndex}_{suffixNum++}";
       
        /// <inheritdoc cref="ExBakeTracker"/>
        public ExBakeTracker BakeTracker { get; }
        
        /// <summary>
        /// If true, AOT compilation should store the compiled expression as a field.
        /// </summary>
        public bool CompileToField { get; set; } = false;
        /// <summary>
        /// If true, AOT compilation should store the compiled expression as a script function. 
        /// </summary>
        public ScriptFnDecl? CompileAsScriptFn { get; set; }

        /// <inheritdoc cref="RootCtx"/>
        public RootCtx() {
            BakeTracker = ServiceLocator.Find<ILangCustomizer>().AOTMode switch {
                AOTMode.Save => new ExBakeTracker.Save(),
                AOTMode.Load => new ExBakeTracker.Load(),
                _ => new ExBakeTracker.None()
            };
        }
    }

    /// <summary>
    /// Handle baking/loading an argument to an expression-reflected function that is not itself an expression
    ///  and cannot be trivially converted into a code representation.
    /// <br/>Returns the argument for chaining convenience.
    /// </summary>
    public T Proxy<T>(T replacee) {
        Ctx.BakeTracker.Proxy(replacee!, typeof(T));
        return replacee;
    }

    /// <inheritdoc cref="Proxy{T}"/>
    public object Proxy(object replacee) => Ctx.BakeTracker.Proxy(replacee, replacee.GetType());
    
    /// <summary>
    /// An argument provided to the expression being compiled. Might be a parameter, function argument, implicit, etc.
    /// </summary>
    public readonly struct Arg {
        /// <summary>
        /// Argument name.
        /// </summary>
        public readonly string name;
        /// <summary>
        /// Type of the expression, eg. typeof(TEx{float}).
        /// </summary>
        public readonly Type texType;
        /// <summary>
        /// Expression representing the argument.
        /// </summary>
        public readonly TEx expr;
        /// <summary>
        /// True if this expression should be prioritized when querying variables by type.
        /// </summary>
        public readonly bool hasTypePriority;

        private Arg(string name, Type texType, TEx expr, bool hasTypePriority) {
            this.name = name;
            this.texType = texType;
            this.expr = expr;
            this.hasTypePriority = hasTypePriority;
        }

        /// <summary>
        /// Create an <see cref="Arg"/> from a <see cref="TEx{T}"/>.
        /// </summary>
        public static Arg FromTEx(string name, TEx expr, bool hasTypePriority) =>
            new(name, expr.GetType(), expr, hasTypePriority);
    }

    /// <summary>
    /// Temporarily add a variable to <see cref="RootCtx"/>.<see cref="RootCtx.AliasStack"/>.
    /// </summary>
    public IDisposable Let(string alias, Ex val) => 
        Ctx.AliasStack.SetDefault(alias).WithPush(val);

    /// <summary>
    /// Arguments provided to this TExArgCtx. Does not carry parent arguments.
    /// </summary>
    public readonly Arg[] Args;
    private readonly Dictionary<string, int> argNameToIndexMap;
    //Maps typeof(TExPI) to index
    private readonly Dictionary<Type, int> argExTypeToIndexMap;
    //Maps typeof(ParametricInfo) to index
    private readonly Dictionary<Type, int> argTypeToIndexMap;

    /// <summary>
    /// Contains the base TExArgCtx from which this was copied
    ///  (if this is a copy with extra arguments).
    /// </summary>
    public TExArgCtx? Parent { get; }
    private readonly RootCtx? ctx;
    /// <inheritdoc cref="RootCtx"/>
    public RootCtx Ctx => ctx ?? Parent?.Ctx ?? throw new StaticException("No RootCtx found");
    
    /// <summary>
    /// Get the linked expression representing the (most-priotized) float argument.
    /// </summary>
    public TEx<float> FloatVal => GetByExprType<TEx<float>>();
    
    /// <summary>
    /// Get the linked expression representing the environment frame argument.
    /// </summary>
    public TEx EnvFrame => GetByType<EnvFrame>();

    /// <inheritdoc cref="TExArgCtx"/>
    public TExArgCtx(params Arg[] args) : this(null, args) { }
    /// <inheritdoc cref="TExArgCtx"/>
    public TExArgCtx(TExArgCtx? parent, params Arg[] args) {
        this.Parent = parent;
        if (parent == null)
            this.ctx = new RootCtx();
        this.Args = args;
        argNameToIndexMap = new Dictionary<string, int>();
        argTypeToIndexMap = new Dictionary<Type, int>();
        argExTypeToIndexMap = new Dictionary<Type, int>();
        for (int ii = 0; ii < args.Length; ++ii) {
            if (argNameToIndexMap.ContainsKey(args[ii].name)) {
                throw new CompileException($"Duplicate argument name: {args[ii].name}");
            }
            argNameToIndexMap[args[ii].name] = ii;
            
            if (!argTypeToIndexMap.TryGetValue(args[ii].expr.type, out var i)
                || !args[i].hasTypePriority
                || args[ii].hasTypePriority) {
                argTypeToIndexMap[args[ii].expr.type] = ii;
            }
            if (!argExTypeToIndexMap.TryGetValue(args[ii].texType, out i)
                || !args[i].hasTypePriority
                || args[ii].hasTypePriority) {
                argExTypeToIndexMap[args[ii].texType] = ii;
            }
        }
    }

    /// <summary>
    /// Get the argument of a specific name and type. Fails if not found.
    /// </summary>
    public TEx<T> GetByName<T>(string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            throw new CompileException($"The variable \"{name}\" is not provided as an argument.");
        return Args[idx].expr as TEx<T> ?? throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{Args.Length}) is not of type {typeof(T).SimpRName()}");
    }
    
    /// <summary>
    /// Try to get the argument of a specific name and type. Returns null if not found.
    /// </summary>
    public TEx<T>? MaybeGetByName<T>(string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            return null;
        return Args[idx].expr is TEx<T> arg ?
            arg :
            //Still throw an error in this case
            throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{Args.Length}) is not of type {typeof(T).SimpRName()}");
    }
    
    /// <inheritdoc cref="GetByName{T}"/>
    public TEx GetByName(Type typ, string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            throw new CompileException($"The variable \"{name}\" is not provided as an argument.");
        return TEx.TExTypeMatches(typ, Args[idx].expr.GetType()) ?
                Args[idx].expr :
                throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{Args.Length}) is not of type {typ.SimpRName()}");
    }
    
    /// <inheritdoc cref="MaybeGetByName{T}"/>
    public TEx? MaybeGetByName(Type typ, string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            return null;
        return TEx.TExTypeMatches(typ, Args[idx].expr.GetType()) ?
            Args[idx].expr :
            //Still throw an error in this case
            throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{Args.Length}) is not of type {typ.SimpRName()}");
    }
    
    /// <inheritdoc cref="GetByType{T}()"/>
    public TEx GetByType<T>(out int idx) {
        if (!argTypeToIndexMap.TryGetValue(typeof(T), out idx))
            throw new CompileException($"No variable of type {typeof(T).SimpRName()} is provided as an argument.");
        return Args[idx].expr;
    }
    
    /// <summary>
    /// Get the most-prioritized argument of a certain type. Throws if none exists.
    /// </summary>
    public TEx GetByType<T>() => GetByType<T>(out _);
    
    /// <summary>
    /// Try to get the most-prioritized argument of a certain type. Returns null if none exists.
    /// </summary>
    public TEx? MaybeGetByType<T>(out int idx) => 
        argTypeToIndexMap.TryGetValue(typeof(T), out idx) ? 
            Args[idx].expr : 
            null;
    
    /// <inheritdoc cref="GetByExprType{T}()"/>
    public Tx GetByExprType<Tx>(out int idx) where Tx : TEx {
        if (!argExTypeToIndexMap.TryGetValue(typeof(Tx), out idx))
            throw new CompileException($"No variable of type {typeof(Tx).SimpRName()} is provided as an argument.");
        return (Tx)Args[idx].expr;
    }
    
    /// <summary>
    /// Get the most-prioritized argument of a certain TEx type. Throws if none exists.
    /// </summary>
    public Tx GetByExprType<Tx>() where Tx : TEx => GetByExprType<Tx>(out _);
    
    /// <summary>
    /// Try to get the most-prioritized argument of a certain TEx type. Returns null if none exists.
    /// </summary>
    public Tx? MaybeGetByExprType<Tx>(out int idx) where Tx : TEx => 
        argExTypeToIndexMap.TryGetValue(typeof(Tx), out idx) ? 
            (Tx) Args[idx].expr : 
            null;

    /// <summary>
    /// Derive a child TExArgCtx from this one, replacing the `idx`'th argument with `newArg`.
    /// </summary>
    public TExArgCtx MakeCopyWith(int idx, Arg newArg) {
        var newargs = Args.ToArray();
        newargs[idx] = newArg;
        return new TExArgCtx(this, newargs);
    }

    /// <summary>
    /// Derive a child TExArgCtx from this one, replacing the first argument of type T with a new argument.
    /// </summary>
    public TExArgCtx MakeCopyForType<T>(out TEx<T> currEx, out TEx<T> copyEx)  {
        currEx = (Ex)GetByType<T>(out int idx);
        copyEx = new TEx<T>();
        return MakeCopyWith(idx, Arg.FromTEx(Args[idx].name, copyEx, Args[idx].hasTypePriority));
    }
    
    /// <summary>
    /// Derive a child TExArgCtx from this one, replacing the first argument of type T with `newEx`.
    /// </summary>
    public TExArgCtx MakeCopyForType<T>(TEx<T> newEx) {
        _ = GetByType<T>(out int idx);
        return MakeCopyWith(idx, Arg.FromTEx(Args[idx].name, newEx, Args[idx].hasTypePriority));
    }
    
    /// <summary>
    /// Derive a child TExArgCtx from this one, replacing the first argument of TEx type T with a new argument.
    /// </summary>
    public TExArgCtx MakeCopyForExType<T>(out T currEx, out T copyEx) where T: TEx, new() {
        currEx = GetByExprType<T>(out int idx);
        copyEx = new T();
        return MakeCopyWith(idx, Arg.FromTEx(Args[idx].name, copyEx, Args[idx].hasTypePriority));
    }
    
    /// <summary>
    /// Derive a child TExArgCtx from this one, replacing the first argument of TEx type T with `newEx`.
    /// </summary>
    public TExArgCtx MakeCopyForExType<T>(T newEx) where T: TEx {
        _ = GetByExprType<T>(out int idx);
        return MakeCopyWith(idx, Arg.FromTEx(Args[idx].name, newEx, Args[idx].hasTypePriority));
    }

    /// <summary>
    /// Derive a copy TExArgCtx from this one, adding a new argument at the end of <see cref="Args"/>.
    /// </summary>
    public TExArgCtx Append(string name, TEx ex, bool hasPriority=true) {
        var newArgs = Args.Append(Arg.FromTEx(name, ex, hasPriority)).ToArray();
        return new TExArgCtx(this, newArgs);
    }
}
