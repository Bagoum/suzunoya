using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;

namespace BagoumLib.Expressions {

/// <summary>
/// A representation of a <see cref="MethodInfo"/> with efficient static lookup methods and
///  convenience methods for expression invocation.
/// </summary>
[PublicAPI]
public class ExFunction {
    /// <summary>
    /// Wrapped <see cref="MethodInfo"/> instance.
    /// </summary>
    public MethodInfo Mi { get; }

    /// <summary>
    /// Create an <see cref="ExFunction"/> with the provided <see cref="MethodInfo"/>.
    /// </summary>
    public ExFunction(MethodInfo mi) {
        this.Mi = mi;
    }

    /// <summary>
    /// Create an expression representing calling a static method on the provided arguments.
    /// </summary>
    public Ex Of(params Ex[] exs) {
        return Ex.Call(null, Mi, exs);
    }

    /// <summary>
    /// Create an expression representing calling an instance method on the provided arguments.
    /// </summary>
    public Ex InstanceOf(Ex instance, params Ex[] exs) {
        return Ex.Call(instance, Mi, exs);
    }
    
    /// <summary>
    /// Disambiguate a method based on its return type.
    /// </summary>
    /// <param name="cls">Type containing the method</param>
    /// <param name="methodName">Method name</param>
    /// <param name="retType">Return type</param>
    public static ExFunction WrapByRetType(Type cls, string methodName, Type retType) {
        foreach (var mi in cls.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                          BindingFlags.Static)) {
            if (mi.Name.Equals(methodName) && mi.ReturnType == retType) 
                    return new ExFunction(mi);
        }
        throw new NotImplementedException(
            $"STATIC ERROR: Method {cls.Name}.{methodName} not found.");
    }
    /// <summary>
    /// Disambiguate a method based on its parameter types.
    /// </summary>
    /// <param name="cls">Type containing the method</param>
    /// <param name="methodName">Method name</param>
    /// <param name="types">Parameter types</param>
    public static ExFunction Wrap(Type cls, string methodName, params Type[] types) {
        foreach (var mi in cls.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                          BindingFlags.Static)) {
            if (mi.Name.Equals(methodName)) {
                var mtypes = mi.GetParameters().Select(x => x.ParameterType).ToArray();
                if (mtypes.Length == types.Length) {
                    for (int ii = 0; ii < mtypes.Length; ++ii) {
                        if (mtypes[ii] != types[ii]) goto Next;
                    }
                    return new ExFunction(mi);
                }
                Next: ;
            }
        }
        throw new NotImplementedException(
            $"STATIC ERROR: Method {cls.Name}.{methodName} not found.");
    }

    private static Dictionary<(Type, string), ExFunction> AnyFunctionCache = new();
    /// <summary>
    /// Get any method on the enclosing type with the corresponding method name.
    /// <br/>This function is cached.
    /// </summary>
    /// <param name="cls"></param>
    /// <param name="methodName"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static ExFunction WrapAny(Type cls, string methodName) {
        if (AnyFunctionCache.TryGetValue((cls, methodName), out var res)) return res;
        var mi = cls.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                           BindingFlags.Static) ?? 
                 throw new NotImplementedException($"STATIC ERROR: Method {cls.Name}.{methodName} not found.");
        return AnyFunctionCache[(cls, methodName)] = new ExFunction(mi);
    }

    /// <summary>
    /// Disambiguate a method based on its parameter types.
    /// </summary>
    /// <typeparam name="C">Type containing the method</typeparam>
    /// <param name="methodName">Method name</param>
    /// <param name="types">Parameter types</param>
    public static ExFunction Wrap<C>(string methodName, params Type[] types) {
        return Wrap(typeof(C), methodName, types);
    }

    /// <summary>
    /// Find any method with the given name.
    /// </summary>
    /// <typeparam name="C">Type containing the method</typeparam>
    /// <param name="methodName">Method name</param>
    public static ExFunction WrapAny<C>(string methodName) => WrapAny(typeof(C), methodName);
    
    /// <summary>
    /// Disambiguate a method based on its parameter types, when all parameters are of the same type.
    /// </summary>
    /// <typeparam name="C">Type containing the method</typeparam>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="methodName">Method name</param>
    /// <param name="typeCt">Number of times the parameter occurs</param>
    public static ExFunction Wrap<C, T>(string methodName, int typeCt = 1) {
        return Wrap<T>(typeof(C), methodName, typeCt);
    }

    /// <summary>
    /// Disambiguate a method based on its parameter types, when all parameters are of the same type.
    /// </summary>
    /// <param name="cls">Type containing the method</param>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="methodName">Method name</param>
    /// <param name="typeCt">Number of times the parameter occurs</param>
    public static ExFunction Wrap<T>(Type cls, string methodName, int typeCt = 1) {
        Type[] types = new Type[typeCt];
        Type ts = typeof(T);
        for (int ii = 0; ii < typeCt; ++ii) {
            types[ii] = ts;
        }
        return Wrap(cls, methodName, types);
    }
    
}
}