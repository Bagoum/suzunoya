using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace BagoumLib.Expressions {

public static class ExExtensions {
    public static Ex Eq(this Ex me, Ex other) => Ex.Equal(me, other);
    public static Ex Is(this Ex me, Ex other) => Ex.Assign(me, other);

    public static Ex Add(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 + f2) :
            Ex.Add(me, other);

    public static Ex Add(this Ex me, float other) => me.Add(Ex.Constant(other));

    public static Ex Sub(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 - f2) :
            Ex.Subtract(me, other);

    public static Ex Sub(this Ex me, float other) => me.Sub(Ex.Constant(other));

    public static Ex Mul(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 * f2) :
            Ex.Multiply(me, other);

    public static Ex Mul(this Ex me, float other) => me.Mul(Ex.Constant(other));

    public static Ex Div(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 / f2) :
            Ex.Divide(me, other);

    public static Ex Div(this Ex me, float other) => me.Div(Ex.Constant(other));
    public static Ex Neg(this Ex me) => Ex.Negate(me);
    public static Ex Complement(this Ex me) => Ex.Constant(1f).Sub(me);

    public static Ex Length(this Ex me) => Ex.ArrayLength(me);

    public static Ex Index(this Ex me, Ex index) => Ex.ArrayAccess(me, index);

    public static Ex Ipp(this Ex me) => Ex.PostIncrementAssign(me);

    public static Ex LT(this Ex me, Ex than) => Ex.LessThan(me, than);
    public static Ex LT0(this Ex me) => Ex.LessThan(me, Ex.Default(me.Type));
    public static Ex GT(this Ex me, Ex than) => Ex.GreaterThan(me, than);
    public static Ex GT0(this Ex me) => Ex.GreaterThan(me, Ex.Default(me.Type));
    public static Ex And(this Ex me, Ex other) => Ex.AndAlso(me, other);
    public static Ex Or(this Ex me, Ex other) => Ex.OrElse(me, other);

    public static Ex Field(this Ex me, string field) => Ex.PropertyOrField(me, field);

    public static Ex As<T>(this Ex me) => me.As(typeof(T));
    public static Ex As(this Ex me, Type t) => 
        me.Type == t ? me : Ex.Convert(me, t);

    
    private static readonly Dictionary<Type, ExFunction> dictContainsMethodCache = new Dictionary<Type, ExFunction>();
    public static Ex DictContains(this Ex dict, Ex key) {
        if (!dictContainsMethodCache.TryGetValue(dict.Type, out var method)) {
            dictContainsMethodCache[dict.Type] = method = 
                ExFunction.Wrap(dict.Type, "ContainsKey", key.Type);
        }
        return method.InstanceOf(dict, key);
    }
    
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

    public static Ex DictSafeGet(this Ex dict, Ex key, Ex deflt) =>
        Ex.Condition(dict.DictContains(key), dict.DictGet(key), deflt);
    public static Ex DictGet(this Ex dict, Ex key) => Ex.Property(dict, "Item", key);
    public static Ex DictSet(this Ex dict, Ex key, Ex value) => Ex.Assign(Ex.Property(dict, "Item", key), value);

    public static bool IsConstant(this Ex ex) => ex.NodeType == ExpressionType.Constant;
    public static bool IsSimplifiable(this Ex ex) => ex.IsConstant() || ex.NodeType == ExpressionType.Parameter;

    public static bool TryAsConst<T>(this Ex ex, out T val) {
        if (ex is ConstantExpression cx && ex.Type == typeof(T)) {
            val = (T) cx.Value!;
            return true;
        }
        val = default!;
        return false;
    }

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