using System;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor.Expressions;


/// <summary>
/// Base class for <see cref="TEx{T}"/> used for type constraints.
/// </summary>
public class TEx {
    public static Func<Type, ParameterExpression, TEx?>? SpecialTypeHandler { get; set; }
    public readonly Ex ex;
    public readonly Type type;
    protected TEx(Ex ex) {
        this.ex = ex;
        this.type = ex.Type;
    }

    public static bool TExTypeMatches(Type basicType, Type texType) {
        if (texType.IsGenericType && texType.GetGenericTypeDefinition() == typeof(TEx<>))
            return texType.GetGenericArguments()[0] == basicType;
        else if (texType.IsSubclassOf(typeof(TEx)))
            return TExTypeMatches(basicType, texType.BaseType!);
        return false;
    }

    //t = typeof(float) or similr
    public static TEx MakeParameter<T>(bool isRef, string name) {
        var t = typeof(T);
        var rt = (isRef) ? t.MakeByRefType() : t;
        var ex = Ex.Parameter(rt, name);
        return SpecialTypeHandler?.Invoke(t, ex) ?? new TEx<T>(ex);
    }

    protected TEx(ExMode mode, Type t, string? name) {
        if (mode == ExMode.RefParameter) {
            t = t.MakeByRefType();
        }
        ex = name == null ? Ex.Parameter(t) : Ex.Parameter(t, name);
        this.type = ex.Type;
    }
    public static implicit operator TEx(Ex ex) {
        return new(ex);
    }
    public static implicit operator Ex(TEx me) {
        return me.ex;
    }
    public static implicit operator ParameterExpression(TEx me) {
        return (ParameterExpression)me.ex;
    }

    public struct ResolveArg {
        public readonly Ex ex;
        public readonly bool reqCopy;
        public readonly string? name;
        
        public ResolveArg(Ex ex, bool reqCopy, string? name = null) {
            this.ex = ex;
            this.reqCopy = reqCopy;
            this.name = name;
        }
    
        public static implicit operator ResolveArg(TEx exx) => new(exx.ex, RequiresCopyOnRepeat(exx.ex));
        public static implicit operator ResolveArg(Ex exx) => new(exx, RequiresCopyOnRepeat(exx));
    }
    
    public static Ex ResolveCopy(Func<Ex[], Ex> func, params ResolveArg[] args) {
        var newvars = ListCache<ParameterExpression>.Get();
        var setters = ListCache<Ex>.Get();
        var usevars = new Ex[args.Length];
        for (int ii = 0; ii < args.Length; ++ii) {
            if (args[ii].reqCopy) {
                //Don't name this, as nested TEx should not overlap
                var copy = Ex.Variable(args[ii].ex.Type, args[ii].name);
                usevars[ii] = copy;
                newvars.Add(copy);
                setters.Add(copy.Is(args[ii].ex));
            } else {
                usevars[ii] = args[ii].ex;
            }
        }
        setters.Add(func(usevars));
        var block = Ex.Block(newvars, setters);
        ListCache<ParameterExpression>.Consign(newvars);
        ListCache<Ex>.Consign(setters);
        return block;
    }

    public static Ex ResolveFieldsMaybeDeconstructNew(Func<Ex[], Ex> func, ResolveArg arg, bool singleUse, params string[] fields) {
        if (!arg.reqCopy)
            return func(fields.Select(f => arg.ex.Field(f)).ToArray());
        if (arg.ex is NewExpression newe)
            return singleUse ?
                func(newe.Arguments.ToArray()) :
                ResolveCopy(func, newe.Arguments.Select((x, i) => new ResolveArg(x, RequiresCopyOnRepeat(x), 
                    $"{(x as ParameterExpression)?.Name ?? "anon"}_{fields[i]}")).ToArray());
        if (IsBlockWithLastNew(arg.ex)) {
            var bex = FlattenNestedBlock((BlockExpression)arg.ex);
            return Ex.Block(bex.Variables, bex.Expressions.Take(bex.Expressions.Count - 1).Append(
                ResolveFieldsMaybeDeconstructNew(func, new(bex.Expressions[^1], true), singleUse, fields)));
        }

        var let = Ex.Variable(arg.ex.Type);
        return Ex.Block(new[] { let }, let.Is(arg.ex), func(fields.Select(f => let.Field(f)).ToArray()));
    }

    private static bool IsBlockWithLastNew(Ex ex) {
        while (ex is BlockExpression bex) {
            ex = bex.Expressions[^1];
            if (ex is NewExpression) return true;
        }
        return false;
    }

    private static BlockExpression FlattenNestedBlock(BlockExpression bex) {
        if (bex.Expressions[^1] is not BlockExpression rbex)
            return bex;
        rbex = FlattenNestedBlock(rbex);
        return Ex.Block(bex.Variables.Concat(rbex.Variables),
            bex.Expressions.Take(bex.Expressions.Count - 1).Concat(rbex.Expressions));
    }
    
    public static Ex ResolveF(TEx<float> t1, Func<TEx<float>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0]), t1);
    public static Ex Resolve<T1>(TEx<T1> t1, Func<TEx<T1>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0]), t1);
    public static Ex Resolve<T1,T2>(TEx<T1> t1, TEx<T2> t2, Func<TEx<T1>, TEx<T2>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1]), t1, t2);
    /// <summary>
    /// Copy the provided expressions into temporary variables that can be reused without recalculating the expression.
    /// </summary>
    public static Ex Resolve<T1,T2,T3>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2]), t1, t2, t3);
    public static Ex Resolve<T1,T2,T3,T4>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3]), t1, t2, t3, t4);
    public static Ex Resolve<T1,T2,T3,T4,T5>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, TEx<T5> t5, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, TEx<T5>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3], x[4]), t1, t2, t3, t4, t5);
    
    public static Ex Resolve<T1,T2,T3,T4,T5,T6>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, TEx<T5> t5, 
        TEx<T6> t6, Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, TEx<T5>, TEx<T6>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3], x[4], x[5]), t1, t2, t3, t4, t5, t6);
    public static Ex Resolve<T1,T2,T3,T4,T5,T6,T7>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, TEx<T5> t5, 
        TEx<T6> t6, TEx<T7> t7, Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, TEx<T5>, TEx<T6>, TEx<T7>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3], x[4], x[5], x[6]), t1, t2, t3, t4, t5, t6, t7);
    
    public static bool RequiresCopyOnRepeat(Ex e) => !(
        e.NodeType == ExpressionType.Parameter ||
        e.NodeType == ExpressionType.Constant ||
        e.NodeType == ExpressionType.MemberAccess ||
        (e.NodeType == ExpressionType.Convert && !RequiresCopyOnRepeat((e as UnaryExpression)!.Operand)));
    
}
/// <summary>
/// A typed expression.
/// <br/>This typing is syntactic sugar: any expression, regardless of type, can be cast as eg. TEx{float}.
/// <br/>However, constructing a parameter expression via TEx{T} will type the expression appropriately.
/// By default, creates a ParameterExpression.
/// </summary>
/// <typeparam name="T">Type of expression eg(float).</typeparam>
public class TEx<T> : TEx {

    public TEx() : this(ExMode.Parameter, null) {}

    public TEx(Ex ex) : base(ex) { }

    public TEx(ExMode m, string? name) : base(m, typeof(T), name) {}
    
    public static implicit operator TEx<T>(Ex ex) {
        return new(ex);
    }

    public static implicit operator TEx<T>(T obj) => Ex.Constant(obj);
}
