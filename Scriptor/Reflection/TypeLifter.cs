using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib.Reflection;
using JetBrains.Annotations;

namespace Scriptor.Reflection;

/// <summary>
/// Helper methods for lifting types and creating func types.
/// </summary>
public static class TypeLifter {
    public static HashSet<Type> FuncifiableReturnTypes { get; } = new();
    public static HashSet<Type> FuncifiableReturnTypeGenerics { get; } = new();
    
    private static readonly Dictionary<(Type toType, Type fromFuncType), Func<object, object, object>> funcConversions =
        new();
    private static readonly Dictionary<(Type, Type), Type> tryFuncifyCache = new();
    private static readonly Dictionary<Type, MethodInfo> funcInvokeCache = new();
    private static readonly Dictionary<(Type, Type), Type> func2TypeCache = new();
    
    /// <summary>
    /// Return the type Func&lt;t1, t2&gt;. Results are cached.
    /// </summary>
    public static Type Func2Type(Type t1, Type t2) {
        if (!func2TypeCache.TryGetValue((t1, t2), out var tf)) {
            tf = func2TypeCache[(t1, t2)] = typeof(Func<,>).MakeGenericType(t1, t2);
        }
        return tf;
    }
    
    /// <summary>
    /// Where arg is the type of a parameter for a recorded function R member(...ARG, ...),
    /// construct the type of the corresponding parameter for the hypothetical function T->R member', such that
    /// the constructed type can be parsed by reflection code with maximum generality.
    /// <br/>In most cases, this is just T->ARG. See comments in the code for more details.
    /// <br/>If the type ARG does not need to be changed, return False (and copy ARG into RES).
    /// </summary>
    public static bool LiftType(Type t, Type arg, out Type res) {
        if (tryFuncifyCache.ContainsKey((t, arg))) {
            //Cached result
            res = tryFuncifyCache[(t, arg)];
            return true;
        }
        //eg. arg = TEx<float>, funcedType = Func<TExArgCtx, TEx<float>
        // func has "real" type (FuncedType) -> TExArgCtx -> (Arg)
        void AddDefuncifier(Type funcedType, Func<object, object, object> func) {
            funcConversions[(arg, funcedType)] = func;
        }
        
        if (FuncifiableReturnTypes.Contains(arg) || (arg.IsGenericType && FuncifiableReturnTypeGenerics.Contains(arg.GetGenericTypeDefinition()))) {
            //Explicitly marked F(B)=T->B.
            var ft = res = Func2Type(t, arg);
            AddDefuncifier(ft, (x, bpi) => FuncInvoke(x, ft, bpi));
        } else if (arg.IsArray && LiftType(t, arg.GetElementType()!, out var ftele)) {
            //Let B=[E]. F(B)=[F(E)].
            res = ftele.MakeArrayType();
            AddDefuncifier(res, (x, bpi) => {
                var oa = x as Array ?? throw new StaticException("Couldn't arrayify");
                var fa = Array.CreateInstance(arg.GetElementType()!, oa.Length);
                for (int oi = 0; oi < oa.Length; ++oi) {
                    fa.SetValue(Defuncify(arg.GetElementType()!, ftele, oa.GetValue(oi), bpi), oi);
                }
                return fa;
            });
        } else if (arg.IsConstructedGenericType && arg.GetGenericTypeDefinition() == typeof(ValueTuple<,>) &&
                   arg.GenericTypeArguments.Any(x => LiftType(t, x, out _))) {
            //Let B=<C,D>. F(B)=<F(C),F(D)>.
            var base_gts = arg.GenericTypeArguments;
            var funced_gts = new Type[base_gts.Length];
            for (int ii = 0; ii < funced_gts.Length; ++ii) {
                funced_gts[ii] = LiftType(t, base_gts[ii], out var gt) ? gt : base_gts[ii];
            }
            res = typeof(ValueTuple<,>).MakeGenericType(funced_gts);
            var tupToArr = typeof(TypeLifter)
                               .GetMethod($"TupleToArr{funced_gts.Length}", BindingFlags.Static | BindingFlags.Public)
                               ?.MakeGenericMethod(funced_gts) ??
                           throw new StaticException("Couldn't find tuple decomposition method");
            AddDefuncifier(res, (x, bpi) => {
                var argarr = tupToArr.Invoke(null, new[] {x}) as object[] ??
                             throw new StaticException("Couldn't decompose tuple to array");
                for (int ii = 0; ii < funced_gts.Length; ++ii)
                    argarr[ii] = Defuncify(base_gts[ii], funced_gts[ii], argarr[ii], bpi);
                return Activator.CreateInstance(arg, argarr);
            });
        } else {
            res = arg;
            return false;
        }
        tryFuncifyCache[(t, arg)] = res;
        return true;
    }
    
        
    /// <summary>
    /// De-funcify a source object whose type involves functions of one argument (eg. [TExArgCtx->tfloat]) into an
    ///  target type (eg. [tfloat]) by applying an object of that argument type (TExArgCtx) to the
    ///  source object according to the function registered in funcConversions.
    /// <br/>If no function is registered, return the source object as-is.
    /// </summary>
    public static object Defuncify(Type targetType, Type sourceFuncType, object sourceObj, object funcArg) {
        if (funcConversions.TryGetValue((targetType, sourceFuncType), out var conv)) 
            return conv(sourceObj, funcArg);
        return sourceObj;
    }

    /// <summary>
    /// Return func(arg).
    /// </summary>
    /// <param name="func">Function to execute.</param>
    /// <param name="funcType">Type of func.</param>
    /// <param name="arg">Argument with which to execute func.</param>
    /// <returns></returns>
    private static object FuncInvoke(object func, Type funcType, object? arg) {
        if (func.GetType() != funcType) //TODO verify usage of this exception for BDSL2
            funcType = func.GetType();
            
        if (!funcInvokeCache.TryGetValue(funcType, out var mi)) {
            mi = funcInvokeCache[funcType] = funcType.GetMethod("Invoke") ??
                                             throw new Exception($"No invoke method found for {funcType.SimpRName()}");
        }
        return mi.Invoke(func, [arg])!;
    }


    [UsedImplicitly]
    public static object[] TupleToArr2<T1, T2>((T1, T2) tup) => [tup.Item1!, tup.Item2!];
}