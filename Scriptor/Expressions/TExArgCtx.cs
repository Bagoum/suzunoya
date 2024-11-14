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
    /// Context that is shared by any copies of this.
    /// </summary>
    public class RootCtx {
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
    
    public readonly struct Arg {
        public readonly string name;
        //typeof(TExPI)
        public readonly Type texType;
        public readonly TEx expr;
        public readonly bool hasTypePriority;

        private Arg(string name, Type texType, TEx expr, bool hasTypePriority) {
            this.name = name;
            this.texType = texType;
            this.expr = expr;
            this.hasTypePriority = hasTypePriority;
        }

        public static Arg FromTEx(string name, TEx expr, bool hasTypePriority) =>
            new(name, expr.GetType(), expr, hasTypePriority);
    }
    
    public class LocalLet : IDisposable {
        private readonly string alias;
        private readonly TExArgCtx ctx;

        public LocalLet(TExArgCtx ctx, string alias, Ex val) {
            this.alias = alias;
            (this.ctx = ctx).Ctx.AliasStack.Push(alias, val);
        }

        public void Dispose() {
            ctx.Ctx.AliasStack.Pop(alias);
        }
    }

    public LocalLet Let(string alias, Ex val) => new(this, alias, val);
    
    public readonly Arg[] Args;
    public IEnumerable<Ex> Expressions => Args.Select(a => (Ex)a.expr);
    private readonly Dictionary<string, int> argNameToIndexMap;
    //Maps typeof(TExPI) to index
    private readonly Dictionary<Type, int> argExTypeToIndexMap;
    //Maps typeof(ParametricInfo) to index
    private readonly Dictionary<Type, int> argTypeToIndexMap;

    private readonly RootCtx? ctx;
    private readonly TExArgCtx? parent;
    public RootCtx Ctx => ctx ?? parent?.Ctx ?? throw new StaticException("No RootCtx found");
    public TEx<float> FloatVal => GetByExprType<TEx<float>>();
    public TEx EnvFrame => GetByType<EnvFrame>();

    public TExArgCtx(params Arg[] args) : this(null, args) { }
    public TExArgCtx(TExArgCtx? parent, params Arg[] args) {
        this.parent = parent;
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

    public TEx<T> GetByName<T>(string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            throw new CompileException($"The variable \"{name}\" is not provided as an argument.");
        return Args[idx].expr as TEx<T> ?? throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{Args.Length}) is not of type {typeof(T).SimpRName()}");
    }
    public TEx<T>? MaybeGetByName<T>(string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            return null;
        return Args[idx].expr is TEx<T> arg ?
            arg :
            //Still throw an error in this case
            throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{Args.Length}) is not of type {typeof(T).SimpRName()}");
    }
    public TEx GetByName(Type typ, string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            throw new CompileException($"The variable \"{name}\" is not provided as an argument.");
        return TEx.TExTypeMatches(typ, Args[idx].expr.GetType()) ?
                Args[idx].expr :
                throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{Args.Length}) is not of type {typ.SimpRName()}");
    }
    
    public TEx? MaybeGetByName(Type typ, string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            return null;
        return TEx.TExTypeMatches(typ, Args[idx].expr.GetType()) ?
            Args[idx].expr :
            //Still throw an error in this case
            throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{Args.Length}) is not of type {typ.SimpRName()}");
    }
    
    public TEx GetByType<T>(out int idx) {
        if (!argTypeToIndexMap.TryGetValue(typeof(T), out idx))
            throw new CompileException($"No variable of type {typeof(T).SimpRName()} is provided as an argument.");
        return Args[idx].expr;
    }
    public TEx GetByType<T>() => GetByType<T>(out _);
    public TEx? MaybeGetByType<T>(out int idx) => 
        argTypeToIndexMap.TryGetValue(typeof(T), out idx) ? 
            Args[idx].expr : 
            null;
    
    public Tx GetByExprType<Tx>(out int idx) where Tx : TEx {
        if (!argExTypeToIndexMap.TryGetValue(typeof(Tx), out idx))
            throw new CompileException($"No variable of type {typeof(Tx).SimpRName()} is provided as an argument.");
        return (Tx)Args[idx].expr;
    }
    public Tx GetByExprType<Tx>() where Tx : TEx => GetByExprType<Tx>(out _);
    public Tx? MaybeGetByExprType<Tx>(out int idx) where Tx : TEx => 
        argExTypeToIndexMap.TryGetValue(typeof(Tx), out idx) ? 
            (Tx) Args[idx].expr : 
            null;

    public TExArgCtx MakeCopyWith(int idx, Arg newArg) {
        var newargs = Args.ToArray();
        newargs[idx] = newArg;
        return new TExArgCtx(this, newargs);
    }

    public TExArgCtx MakeCopyForType<T>(out TEx<T> currEx, out TEx<T> copyEx)  {
        currEx = (Ex)GetByType<T>(out int idx);
        copyEx = new TEx<T>();
        return MakeCopyWith(idx, Arg.FromTEx(Args[idx].name, copyEx, Args[idx].hasTypePriority));
    }
    
    public TExArgCtx MakeCopyForType<T>(TEx<T> newEx) {
        _ = GetByType<T>(out int idx);
        return MakeCopyWith(idx, Arg.FromTEx(Args[idx].name, newEx, Args[idx].hasTypePriority));
    }
    
    public TExArgCtx MakeCopyForExType<T>(out T currEx, out T copyEx) where T: TEx, new() {
        currEx = GetByExprType<T>(out int idx);
        copyEx = new T();
        return MakeCopyWith(idx, Arg.FromTEx(Args[idx].name, copyEx, Args[idx].hasTypePriority));
    }
    
    public TExArgCtx MakeCopyForExType<T>(T newEx) where T: TEx {
        _ = GetByExprType<T>(out int idx);
        return MakeCopyWith(idx, Arg.FromTEx(Args[idx].name, newEx, Args[idx].hasTypePriority));
    }

    public TExArgCtx Append(string name, TEx ex, bool hasPriority=true) {
        var newArgs = Args.Append(Arg.FromTEx(name, ex, hasPriority)).ToArray();
        return new TExArgCtx(this, newArgs);
    }
    
    public Ex When(Func<TExArgCtx, TEx<bool>> pred, Ex then) => Ex.IfThen(pred(this), then);
}
