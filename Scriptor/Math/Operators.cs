using System.Linq.Expressions;
using BagoumLib.Expressions;
using Scriptor.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static Scriptor.Expressions.ExHelpers;
using static Scriptor.Expressions.ExMHelpers;
#pragma warning disable CS8981
using tfloat = Scriptor.Expressions.TEx<float>;
using tbool = Scriptor.Expressions.TEx<bool>;

namespace Scriptor.Math;
/// <summary>
/// Functions that are operators in BDSL2.
/// </summary>
[Reflect]
public class ExMOperators {
    /// <summary>
    /// Returns x.
    /// </summary>
    [BDSL2Operator]
    public static TEx<T> ReturnSame<T>(TEx<T> x) => x;
    
    /// <summary>
    /// Returns -x.
    /// </summary>
    [BDSL2Operator]
    public static TEx<T> Negate<T>(TEx<T> x) {
        if ((Expression)x is ConstantExpression ce) {
            return ce.Value switch {
                float f => ExC(-f),
                int i => ExC(-i),
                _ => Expression.Negate(x)
            };
        }
        return Expression.Negate(x);
    }
    
    /// <summary>
    /// Return true iff the argument is false.
    /// </summary>
    [BDSL2Operator]
    public static tbool Not(tbool pred) => Ex.Not(pred);
    
    
    /// <summary>
    /// Returns (bas)^(exp).
    /// </summary>
    [Alias("^")] [WarnOnStrict] [Operator] [BDSL2Operator]
    public static TEx<T> Pow<T>(TEx<T> bas, TEx<T> exp) => OfDTD<T>(_Pow, bas, exp);
    private static readonly ExFunction _Pow = ExFunction.Wrap<double>(typeof(System.Math), "Pow", 2);
    
    /// <summary>
    /// Returns one function raised to the power of the other, subtracted by the first function. (Alias: ^- bas exp)
    /// Useful for getting polynomial curves that start at zero, eg. ^- t 1.1
    /// </summary>
    /// <returns></returns>
    [Alias("^-")] [Operator] [BDSL2Operator]
    public static tfloat PowSub(tfloat bas, tfloat exp) {
        var val = VFloat();
        return Ex.Block(new[] { val },
            Ex.Assign(val, bas),
            Pow(val, exp).Sub(val)
        );
    }

    /// <summary>
    /// Returns one number raised to the power of the other.
    /// If bas is negative, then returns - (-bas)^exp. This allows fractional powers on negatives.
    /// (Alias: ^^ bas exp)
    /// </summary>
    /// <param name="bas">Base</param>
    /// <param name="exp">Exponent</param>
    /// <returns></returns>
    [Alias("^^")] [Operator] [BDSL2Operator]
    public static tfloat NPow(tfloat bas, tfloat exp) => TEx.Resolve(bas, x =>
        Ex.Condition(Ex.LessThan(x, E0),
            Ex.Negate(Pow(Ex.Negate(x), exp)),
            Pow(x, exp)));
    
    /// <inheritdoc cref="Expression.Modulo(Ex,Ex)"/>
    [BDSL2Operator] [RestrictTypes(0, typeof(float), typeof(int))]
    public static TEx<T> Modulo<T>(TEx<T> x, TEx<T> by) => Ex.Modulo(x, by);

    /// <summary>
    /// Multiply a vectype by a number.
    /// </summary>
    [Alias("*")] [WarnOnStrict] [Operator] [BDSL2Operator] [BDSL2MULTIPLY_OPERATOR(false)]
    public static TEx<T> Mul<T>(tfloat x, TEx<T> y) => x.Mul(y);

    /// <inheritdoc cref="Mul{T}"/>
    [BDSL2Operator] [BDSL2MULTIPLY_OPERATOR(false)]
    public static TEx<T> MulRev<T>(TEx<T> y, tfloat x) => x.Mul(y);

    /// <inheritdoc cref="Mul{T}"/>
    [BDSL2Operator] [BDSL2MULTIPLY_OPERATOR(true)]
    public static tfloat MulFloat(tfloat x, tfloat y) => x.Mul(y);

    /// <inheritdoc cref="Mul{T}"/>
    [BDSL2Operator] [BDSL2MULTIPLY_OPERATOR(true)]
    public static TEx<int> MulInt(TEx<int> x, TEx<int> y) => x.Mul(y);
    
    /// <summary>
    /// Divide a vectype by a number. Alias: / x y
    /// </summary>
    /// <returns></returns>
    [Alias("/")] [WarnOnStrict] [Operator] [BDSL2Operator]
    public static TEx<T> Div<T>(TEx<T> x, tfloat y) => x.Div(y);
    
    /// <summary>
    /// Add two vectypes.
    /// </summary>
    [Alias("+")] [WarnOnStrict] [Operator] [BDSL2Operator]
    public static TEx<T> Add<T>(TEx<T> x, TEx<T> y) => x.Add(y);
    
    /// <summary>
    /// Subtract two vectypes.
    /// </summary>
    [Alias("-")] [WarnOnStrict] [Operator] [BDSL2Operator]
    public static TEx<T> Sub<T>(TEx<T> x, TEx<T> y) => x.Sub(y);


    /// <summary>
    /// Return true iff both arguments are true.
    /// </summary>
    /// <param name="pr1">First predicate</param>
    /// <param name="pr2">Second predicate</param>
    /// <returns></returns>
    [Alias("&")] [WarnOnStrict] [Operator] [BDSL2Operator]
    public static tbool And(tbool pr1, tbool pr2) {
        return Ex.AndAlso(pr1, pr2);
    }

    /// <summary>
    /// Return true iff one or more arguments are true.
    /// </summary>
    /// <param name="pr1">First predicate</param>
    /// <param name="pr2">Second predicate</param>
    /// <returns></returns>
    [Alias("|")] [WarnOnStrict] [Operator] [BDSL2Operator]
    public static tbool Or(tbool pr1, tbool pr2) {
        return Ex.OrElse(pr1, pr2);
    }

    /// <summary>
    /// Return true iff the first argument is equal to the second.
    /// </summary>
    [Alias("=")] [Operator] [BDSL2Operator] [BDSL1AutoSpecialize(0, typeof(float))]
    public static tbool Eq<T>(TEx<T> b1, TEx<T> b2) => Ex.Equal(b1, b2);

    /// <summary>
    /// Return true iff the first argument is not equal to the second.
    /// </summary>
    [Alias("=/=")] [Operator] [BDSL2Operator] [BDSL1AutoSpecialize(0, typeof(float))]
    public static tbool Neq<T>(TEx<T> b1, TEx<T> b2) => Ex.NotEqual(b1, b2);


    /// <summary>
    /// Return true iff the first argument is greater than the second.
    /// </summary>
    /// <param name="b1">First BPY function</param>
    /// <param name="b2">Second BPY function</param>
    /// <returns></returns>
    [Alias(">")] [Operator] [BDSL2Operator] [RestrictTypes(0, typeof(float), typeof(int))] [BDSL1AutoSpecialize(0, typeof(float))]
    public static tbool Gt<T>(TEx<T> b1, TEx<T> b2) {
        return Ex.GreaterThan(b1, b2);
    }

    /// <summary>
    /// Return true iff the first argument is greater than or equal to the second.
    /// </summary>
    /// <param name="b1">First BPY function</param>
    /// <param name="b2">Second BPY function</param>
    /// <returns></returns>
    [Alias(">=")] [Operator] [BDSL2Operator] [RestrictTypes(0, typeof(float), typeof(int))] [BDSL1AutoSpecialize(0, typeof(float))]
    public static tbool Geq<T>(TEx<T> b1, TEx<T> b2)  {
        return Ex.GreaterThanOrEqual(b1, b2);
    }

    /// <summary>
    /// Return true iff the first argument is less than the second.
    /// </summary>
    /// <param name="b1">First BPY function</param>
    /// <param name="b2">Second BPY function</param>
    /// <returns></returns>
    [Alias("<")] [Operator] [BDSL2Operator] [RestrictTypes(0, typeof(float), typeof(int))] [BDSL1AutoSpecialize(0, typeof(float))]
    public static tbool Lt<T>(TEx<T> b1, TEx<T> b2) {
        return Ex.LessThan(b1, b2);
    }

    /// <summary>
    /// Return true iff the first argument is less than or equal to the second.
    /// </summary>
    /// <param name="b1">First BPY function</param>
    /// <param name="b2">Second BPY function</param>
    /// <returns></returns>
    [Alias("<=")] [Operator] [BDSL2Operator] [RestrictTypes(0, typeof(float), typeof(int))] [BDSL1AutoSpecialize(0, typeof(float))]
    public static tbool Leq<T>(TEx<T> b1, TEx<T> b2) {
        return Ex.LessThanOrEqual(b1, b2);
    }
    
    /// <summary>
    /// Array indexing operator arr[index].
    /// </summary>
    [BDSL2Operator]
    public static TEx<T> ArrayIndex<T>(TEx<T[]> arr, TEx<int> index) => arr.ex.Index(index);
}
