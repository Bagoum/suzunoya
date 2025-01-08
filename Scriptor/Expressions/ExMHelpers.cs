using System.Linq;
using System.Numerics;
using BagoumLib.Expressions;
using BagoumLib.Mathematics;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using static Scriptor.Expressions.ExHelpers;
using static BagoumLib.Mathematics.BMath;

namespace Scriptor.Expressions;

/// <summary>
/// Expression helpers for mathematical operations.
/// </summary>
[PublicAPI]
public static class ExMHelpers {
    /// <inheritdoc cref="HPI"/>
    public static readonly Ex hpi = Ex.Constant(HPI);
    /// <inheritdoc cref="PI"/>
    public static readonly Ex pi = Ex.Constant(PI);
    /// <summary>Negative pi</summary>
    public static readonly Ex npi = Ex.Constant(-PI);
    /// <inheritdoc cref="TAU"/>
    public static readonly Ex tau = Ex.Constant(TAU);
    /// <inheritdoc cref="TWAU"/>
    public static readonly Ex twau = ExC(TWAU);
    /// <inheritdoc cref="BMath.degRad"/>
    public static readonly Ex degRad = ExC(BMath.degRad);
    /// <inheritdoc cref="BMath.radDeg"/>
    public static readonly Ex radDeg = ExC(BMath.radDeg);
    /// <inheritdoc cref="PHI"/>
    public static readonly Ex phi = Ex.Constant(PHI);
    /// <inheritdoc cref="IPHI"/>
    public static readonly Ex iphi = Ex.Constant(IPHI);
    /// <summary>360 * phi</summary>
    public static readonly Ex phi360 = Ex.Constant(360f * PHI);

    /// <summary>0f</summary>
    public static readonly Ex E0 = Ex.Constant(0.0f);
    /// <summary>0.5f</summary>
    public static readonly Ex E05 = Ex.Constant(0.5f);
    /// <summary>0.25f</summary>
    public static readonly Ex E025 = Ex.Constant(0.25f);
    /// <summary>1f</summary>
    public static readonly Ex E1 = Ex.Constant(1.0f);
    /// <summary>2f</summary>
    public static readonly TEx<float> E2 = Ex.Constant(2.0f);
    /// <summary>-1f</summary>
    public static readonly Ex EN1 = Ex.Constant(-1f);
    /// <summary>-2f</summary>
    public static readonly Ex EN2 = Ex.Constant(-2f);
    /// <summary>-0.5f</summary>
    public static readonly Ex EN05 = Ex.Constant(-0.5f);
    
    /// <inheritdoc cref="Ex.Constant(object)"/>
    public static Ex ExC(object x) => Ex.Constant(x);
    
    /// <summary>
    /// For a function f of type double->double, call it with any numeric argument
    /// and cast the result back to type float.
    /// <br/>This is what most Unity Mathf functions do internally.
    /// </summary>
    public static Ex OfDFD(ExFunction f, Ex arg) => f.Of(arg.Cast<double>()).Cast<float>();

    /// <summary>
    /// For a function f of type (double,double...)->double, call it with any numeric arguments
    /// and cast the result back to type float.
    /// </summary>
    public static Ex OfDFD(ExFunction f, params Ex[] args) => OfDTD<float>(f, args);
    
    /// <summary>
    /// For a function f of type (double,double...)->double, call it with any numeric arguments
    /// and cast the result back to type T.
    /// </summary>
    public static Ex OfDTD<T>(ExFunction f, params Ex[] args) {
        for (int ii = 0; ii < args.Length; ++ii) args[ii] = args[ii].Cast<double>();
        return f.Of(args).Cast<T>();
    }
}