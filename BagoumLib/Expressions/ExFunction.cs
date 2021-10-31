using System;
using System.Linq;
using System.Reflection;
using Ex = System.Linq.Expressions.Expression;

namespace BagoumLib.Expressions {

public class ExFunction {
    private readonly MethodInfo mi;

    public ExFunction(MethodInfo mi) {
        this.mi = mi;
    }

    public Ex Of(params Ex[] exs) {
        return Ex.Call(null, mi, exs);
    }

    public Ex InstanceOf(Ex instance, params Ex[] exs) {
        return Ex.Call(instance, mi, exs);
    }
    
    
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

    public static ExFunction Wrap<C>(string methodName, params Type[] types) {
        return Wrap(typeof(C), methodName, types);
    }

    public static ExFunction Wrap<C>(string methodName) {
        return Wrap<C>(methodName, Array.Empty<Type>());
    }

    public static ExFunction Wrap<C, T>(string methodName, int typeCt = 1) {
        return Wrap<T>(typeof(C), methodName, typeCt);
    }

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