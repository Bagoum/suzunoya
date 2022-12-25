using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using BagoumLib.DataStructures;
using JetBrains.Annotations;

namespace BagoumLib.Mathematics {
/// <summary>
/// A repository for generic addition, scalar multiplication, pointwise multiplication, and lerp operators.
/// </summary>
[PublicAPI]
public static class GenericOps {
    private static readonly Dictionary<Type, object> lerpers = new() {
        {typeof(float), (Func<float, float, float, float>) BMath.LerpU},
        {typeof(Vector2), (Func<Vector2, Vector2, float, Vector2>) ((a, b, t) => a * (1 - t) + b * t)},
        {typeof(Vector3), (Func<Vector3, Vector3, float, Vector3>) ((a, b, t) => a * (1 - t) + b * t)},
        {typeof(Vector4), (Func<Vector4, Vector4, float, Vector4>) ((a, b, t) => a * (1 - t) + b * t)},
        {typeof(FColor), (Func<FColor, FColor, float, FColor>) FColor.LerpU},
        {typeof(Quaternion), (Func<Quaternion, Quaternion, float, Quaternion>) ((a, b, t) => BMath.Slerp(in a, in b, t))},
    };
    private static readonly Dictionary<Type, object> addOps = new() {
        {typeof(int), (0, (Func<int, int, int>) ((x, y) => x + y))},
        {typeof(float), (0f, (Func<float, float, float>) ((x, y) => x + y))},
        {typeof(Vector2), (Vector2.Zero, (Func<Vector2, Vector2, Vector2>) ((x, y) => x + y))},
        {typeof(Vector3), (Vector3.Zero, (Func<Vector3, Vector3, Vector3>) ((x, y) => x + y))},
        {typeof(Vector4), (Vector4.Zero, (Func<Vector4, Vector4, Vector4>) ((x, y) => x + y))},
        {typeof(FColor), (new FColor(0, 0, 0, 0), (Func<FColor, FColor, FColor>) ((x, y) => x + y))},
        //quaternion addition is not meaningful
    };
    private static readonly Dictionary<Type, object> multiplyOps = new() {
        {typeof(int), (Func<int, int, int>) ((x, y) => x * y)},
        {typeof(float), (Func<float, float, float>) ((x, y) => x * y)},
        {typeof(Vector2), (Func<Vector2, float, Vector2>) ((x, y) => x * y)},
        {typeof(Vector3), (Func<Vector3, float, Vector3>) ((x, y) => x * y)},
        {typeof(Vector4), (Func<Vector4, float, Vector4>) ((x, y) => x * y)},
        {typeof(FColor), (Func<FColor, float, FColor>) ((x, y) => x * y)},
        //scalar quaternion multiplication is not meaningful
        
        {typeof(AxisAngle), (Func<AxisAngle, float, AxisAngle>) ((x, y) => y * x)}
    };
    private static readonly Dictionary<Type, object> vecMultiplyOps = new() {
        {typeof(int), (1, (Func<int, int, int>) ((x, y) => x * y))},
        {typeof(float), (1f, (Func<float, float, float>) ((x, y) => x * y))},
        {typeof(Vector2), (Vector2.One, (Func<Vector2, Vector2, Vector2>) ((x, y) => x * y))},
        {typeof(Vector3), (Vector3.One, (Func<Vector3, Vector3, Vector3>) ((x, y) => x * y))},
        {typeof(Vector4), (Vector4.One, (Func<Vector4, Vector4, Vector4>) ((x, y) => x * y))},
        {typeof(FColor), (new FColor(1, 1, 1, 1), (Func<FColor, FColor, FColor>) ((x, y) => x * y))},
        {typeof(Quaternion), (Quaternion.Identity, (Func<Quaternion, Quaternion, Quaternion>)((x, y) => x * y))},
    };
    
    /// <summary>
    /// Get an (unclamped) lerping function for the provided type.
    /// </summary>
    /// <typeparam name="T">A mathematical type such as float, Vector2, Color, etc.</typeparam>
    public static Func<T, T, float, T> GetLerp<T>() => lerpers.TryGetValue(typeof(T), out var l) ?
        (Func<T, T, float, T>)l :
        throw new Exception($"No lerp handling for type {typeof(T)}");
    
    /// <summary>
    /// Get an addition function for the provided type.
    /// </summary>
    /// <typeparam name="T">A mathematical type such as float, Vector2, Color, etc.</typeparam>
    public static (T zero, Func<T, T, T> add) GetAddOp<T>() => addOps.TryGetValue(typeof(T), out var l) ?
        ((T, Func<T, T, T>))l :
        throw new Exception($"No add handling for type {typeof(T)}");

    /// <summary>
    /// Get n scalar multiplication function for the provided type.
    /// </summary>
    /// <typeparam name="T">A mathematical type such as float, Vector2, Color, etc.</typeparam>
    public static Func<T, float, T> GetMulOp<T>() => multiplyOps.TryGetValue(typeof(T), out var l) ?
        (Func<T, float, T>)l :
        throw new Exception($"No multiply handling for type {typeof(T)}");
    /// <summary>
    /// Get an elementwise multiplication function for the provided type.
    /// </summary>
    /// <typeparam name="T">A mathematical type such as float, Vector2, Color, etc.</typeparam>
    public static (T zero, Func<T, T, T> add) GetVecMulOp<T>() => vecMultiplyOps.TryGetValue(typeof(T), out var l) ?
        ((T, Func<T, T, T>))l :
        throw new Exception($"No vec-multiply handling for type {typeof(T)}");

    /// <summary>
    /// Register a lerping function for a given type. Function should be an unclamped lerp.
    /// </summary>
    public static void RegisterLerper<T>(Func<T, T, float, T> lerper) => lerpers[typeof(T)] = lerper;

    /// <summary>
    /// Register an addition function for a given type.
    /// </summary>
    public static void RegisterAdder<T>((T, Func<T, T, T>) addOp) => addOps[typeof(T)] = addOp;
    
    /// <summary>
    /// Register a scalar multiplication function for a given type.
    /// </summary>
    public static void RegisterMultiplier<T>(Func<T, float, T> mulOp) => multiplyOps[typeof(T)] = mulOp;
    
    /// <summary>
    /// Register an elementwise multiplication function for a given type.
    /// </summary>
    public static void RegisterVecMultiplier<T>((T, Func<T, T, T>) vecMulOp) => 
        vecMultiplyOps[typeof(T)] = vecMulOp;

    /// <summary>
    /// Register lerp, addition, scalar/elementwise multiplication operations for a given type.
    /// </summary>
    public static void RegisterType<T>(Func<T, T, float, T> lerper, Func<T, float, T> mulOp, 
        (T, Func<T, T, T>) addOp, (T, Func<T, T, T>) vecMulOp) {
        RegisterLerper(lerper);
        RegisterMultiplier(mulOp);
        RegisterAdder(addOp);
        RegisterVecMultiplier(vecMulOp);
    }
}
}