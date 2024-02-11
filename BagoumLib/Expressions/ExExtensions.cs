using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;

namespace BagoumLib.Expressions {

/// <summary>
/// Extensions and helpers for expression trees.
/// </summary>
[PublicAPI]
public static class ExExtensions {
    public static readonly ExFunction StringAdd = 
        ExFunction.Wrap<string>(nameof(string.Concat), typeof(string), typeof(string));
    
    /// <summary>
    /// Ex.Equal (equality comparison)
    /// </summary>
    public static Ex Eq(this Ex me, Ex other) => Ex.Equal(me, other);
    
    /// <summary>
    /// Ex.Assign (variable assignment)
    /// </summary>
    public static Ex Is(this Ex me, Ex other) => Ex.Assign(me, other);

    /// <summary>
    /// Ex.Add (addition operator). If both sides are constants,
    /// then produces a constant expression instead of an addition expression.
    /// </summary>
    public static Ex Add(this Ex me, Ex other) {
        if (me.TryAsConst(out float f1) && other.TryAsConst(out float f2))
            return Ex.Constant(f1 + f2);
        //string add is a c# compiler trick
        if (me.Type == typeof(string) && other.Type == typeof(string)) {
            if (me.TryAsConst(out string a) && other.TryAsConst(out string b))
                return Ex.Constant(a + b);
            return StringAdd.Of(me, other);
        }
        return Ex.Add(me, other);
    }

    /// <inheritdoc cref="Add(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)"/>
    public static Ex Add(this Ex me, float other) => me.Add(Ex.Constant(other));

    
    /// <summary>
    /// Ex.Subtract (subtraction operator). If both sides are constants,
    /// then produces a constant expression instead of a subtraction expression.
    /// </summary>
    public static Ex Sub(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 - f2) :
            Ex.Subtract(me, other);

    /// <inheritdoc cref="Sub(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)"/>
    public static Ex Sub(this Ex me, float other) => me.Sub(Ex.Constant(other));

    /// <summary>
    /// Ex.Multiply (multiplication operator). If both sides are constants,
    /// then produces a constant expression instead of a multiplication expression.
    /// </summary>
    public static Ex Mul(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 * f2) :
            Ex.Multiply(me, other);

    /// <inheritdoc cref="Mul(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)"/>
    public static Ex Mul(this Ex me, float other) => me.Mul(Ex.Constant(other));

    
    /// <summary>
    /// Ex.Divide (division operator). If both sides are constants,
    /// then produces a constant expression instead of a division expression.
    /// </summary>
    public static Ex Div(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 / f2) :
            Ex.Divide(me, other);
    
    /// <inheritdoc cref="Div(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)"/>
    public static Ex Div(this Ex me, float other) => me.Div(Ex.Constant(other));
    
    /// <summary>
    /// Ex.Negate (arithmetic negation)
    /// </summary>
    public static Ex Neg(this Ex me) => Ex.Negate(me);
    
    /// <summary>
    /// The subtraction expression (1 - me)
    /// </summary>
    public static Ex Complement(this Ex me) => Ex.Constant(1f).Sub(me);

    /// <summary>
    /// Ex.ArrayLength
    /// </summary>
    public static Ex Length(this Ex me) => Ex.ArrayLength(me);

    /// <summary>
    /// Ex.ArrayAccess
    /// </summary>
    public static Ex Index(this Ex me, Ex index) => Ex.ArrayAccess(me, index);

    /// <summary>
    /// Ex.PostIncrementAssign (me++)
    /// </summary>
    public static Ex Ipp(this Ex me) => Ex.PostIncrementAssign(me);

    /// <summary>
    /// Ex.LessThan
    /// </summary>
    public static Ex LT(this Ex me, Ex than) => Ex.LessThan(me, than);
    
    /// <summary>
    /// The boolean expression for (me &lt; 0)
    /// </summary>
    public static Ex LT0(this Ex me) => Ex.LessThan(me, Ex.Default(me.Type));
    
    /// <summary>
    /// Ex.GreaterThan
    /// </summary>
    public static Ex GT(this Ex me, Ex than) => Ex.GreaterThan(me, than);
    
    /// <summary>
    /// The boolean expression for (me &gt; 0)
    /// </summary>
    public static Ex GT0(this Ex me) => Ex.GreaterThan(me, Ex.Default(me.Type));
    
    /// <summary>
    /// Ex.AndAlso (short-circuit and)
    /// </summary>
    public static Ex And(this Ex me, Ex other) => Ex.AndAlso(me, other);
    
    /// <summary>
    /// Ex.OrElse (short-circuit else)
    /// </summary>
    public static Ex Or(this Ex me, Ex other) => Ex.OrElse(me, other);

    /// <summary>
    /// Ex.PropertyOrField
    /// </summary>
    public static Ex Field(this Ex me, string field) => Ex.PropertyOrField(me, field);
    
    /// <summary>
    /// Ex.Convert
    /// </summary>
    public static Ex Cast<T>(this Ex me) => me.Cast(typeof(T));
    
    /// <summary>
    /// Ex.Convert
    /// </summary>
    public static Ex Cast(this Ex me, Type t) => 
        me.Type == t ? me : Ex.Convert(me, t);
    
    /// <summary>
    /// Ex.TypeAs
    /// </summary>
    public static Ex As<T>(this Ex me) => me.As(typeof(T));
    
    /// <summary>
    /// Ex.TypeAs
    /// </summary>
    public static Ex As(this Ex me, Type t) =>
        me.Type == t ? me : Ex.TypeAs(me, t);
    
    private static readonly Dictionary<Type, ExFunction> dictContainsMethodCache = new Dictionary<Type, ExFunction>();
    
    /// <summary>
    /// The method call (dict.ContainsKey(key))
    /// </summary>
    public static Ex DictContains(this Ex dict, Ex key) {
        if (!dictContainsMethodCache.TryGetValue(dict.Type, out var method)) {
            dictContainsMethodCache[dict.Type] = method = 
                ExFunction.Wrap(dict.Type, "ContainsKey", key.Type);
        }
        return method.InstanceOf(dict, key);
    }
    
    /// <summary>
    /// The expression for (dict.ContainsKey(key) ? dict[key] : throw new Exception(err))
    /// </summary>
    public static Ex DictGetOrThrow(this Ex dict, Ex key, string err) {
        return Ex.Block(
            Ex.IfThen(Ex.Not(dict.DictContains(key)), Ex.Throw(Ex.Constant(new Exception(err)))),
            dict.DictGet(key)
        );
        /*
        return Ex.Condition(ExUtils.DictContains<K, V>(dict, key), dict.DictGet(key), Ex.Block(
            Ex.Throw(Ex.Constant(new Exception(err))),
            dict.DictGet(key)
        ));*/
    }

    /// <summary>
    /// The expression for (dict.ContainsKey(key) ? dict[key] : deflt)
    /// </summary>
    public static Ex DictSafeGet(this Ex dict, Ex key, Ex deflt) =>
        Ex.Condition(dict.DictContains(key), dict.DictGet(key), deflt);
    
    /// <summary>
    /// The expression for (dict[key]). Note that this allows read and write.
    /// </summary>
    public static Ex DictGet(this Ex dict, Ex key) => Ex.Property(dict, "Item", key);
    
    /// <summary>
    /// The expression for (dict[key] = value)
    /// </summary>
    public static Ex DictSet(this Ex dict, Ex key, Ex value) => Ex.Assign(Ex.Property(dict, "Item", key), value);

    /// <summary>
    /// True iff the expression is a constant value.
    /// </summary>
    /// <param name="ex"></param>
    /// <returns></returns>
    public static bool IsConstant(this Ex ex) => ex.NodeType == ExpressionType.Constant;
    
    /// <summary>
    /// True iff the expression is a constant or a parameter.
    /// </summary>
    public static bool IsSimplifiable(this Ex ex) => ex.IsConstant() || ex.NodeType == ExpressionType.Parameter;

    /// <summary>
    /// Get the value of the expression if it is a <see cref="ConstantExpression"/> with type T.
    /// </summary>
    public static bool TryAsConst<T>(this Ex ex, out T val) {
        if (ex is ConstantExpression cx && ex.Type == typeof(T)) {
            val = (T) cx.Value!;
            return true;
        }
        val = default!;
        return false;
    }

    /// <summary>
    /// Get the value of the expression if it is a <see cref="ConstantExpression"/>.
    /// </summary>
    public static bool TryAsAnyConst(this Ex ex, out object? val) {
        if (ex is ConstantExpression cx && ex.Type.IsValueType) {
            val = cx.Value;
            return true;
        }
        val = default!;
        return false;
    }
    
    
}
}