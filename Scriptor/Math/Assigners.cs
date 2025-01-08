using System;
using System.Collections.Generic;
using BagoumLib.Reflection;
using Scriptor.Expressions;
using static Scriptor.Expressions.ExMHelpers;

namespace Scriptor.Math;
using Ex = System.Linq.Expressions.Expression;

/// <summary>
/// This class contains functions related to assignment.
/// It is not reflected as it is only for use with BDSL2, which calls them explicitly.
/// </summary>
[DontReflect]
public static class ExMAssign {
    /// <summary>
    /// Assign x = y.
    /// </summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> Assign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, y);
    
    /// <summary>
    /// Assign x = y. Used only for variable initialization.
    /// </summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> VariableInitialize<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, y);
    
    //Ex.AddAssign, etc do not work on struct/class fields, so we can't use them directly
    /// <summary>x += y</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> AddAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Add(x, y));
    
    /// <summary>x -= y</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> SubAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Subtract(x, y));
    
    /// <summary>x *= y</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> MulAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Multiply(x, y));
    
    /// <summary>x /= y</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> DivAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Divide(x, y));
    
    /// <summary>x %= y</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> ModAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Modulo(x, y));
    
    /// <summary>x &amp;= y</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> AndAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.And(x, y));
    
    /// <summary>x |= y</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> OrAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Or(x, y));

    private static readonly Dictionary<Type, Ex> ones = new() {
        {typeof(int), ExC(1)},
        {typeof(float), ExC(1f)},
        {typeof(double), ExC(1.0)}
    };
    private static Ex GetOne(Type t) {
        if (ones.TryGetValue(t, out var one))
            return one;
        throw new Exception($"Increments and decrements are not supported on the type {t.RName()}.");
    }
    
    /// <summary>x++</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PostIncrement<T>(TEx<T> x) => AddAssign(x, GetOne(typeof(T))).Sub(GetOne(typeof(T)));

    /// <summary>++x</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PreIncrement<T>(TEx<T> x) => AddAssign(x, GetOne(typeof(T)));
    
    /// <summary>x--</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PostDecrement<T>(TEx<T> x) => SubAssign(x, GetOne(typeof(T))).Add(GetOne(typeof(T)));
    
    /// <summary>--x</summary>
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PreDecrement<T>(TEx<T> x) => SubAssign(x, GetOne(typeof(T)));
}