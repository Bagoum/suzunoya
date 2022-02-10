using System;
using System.Collections.Generic;
using System.Numerics;
using BagoumLib.DataStructures;

namespace BagoumLib.Mathematics {
public static class GenericOps {
    private static readonly Dictionary<Type, object> lerpers = new() {
        {typeof(float), (Func<float, float, float, float>) BMath.LerpU},
        {typeof(Vector2), (Func<Vector2, Vector2, float, Vector2>) ((a, b, t) => a * (1 - t) + b * t)},
        {typeof(Vector3), (Func<Vector3, Vector3, float, Vector3>) ((a, b, t) => a * (1 - t) + b * t)},
        {typeof(Vector4), (Func<Vector4, Vector4, float, Vector4>) ((a, b, t) => a * (1 - t) + b * t)},
        {typeof(FColor), (Func<FColor, FColor, float, FColor>) FColor.LerpU},
    };
    private static readonly Dictionary<Type, object> addOps = new() {
        {typeof(int), (0, (Func<int, int, int>) ((x, y) => x + y))},
        {typeof(float), (0f, (Func<float, float, float>) ((x, y) => x + y))},
        {typeof(Vector2), (Vector2.Zero, (Func<Vector2, Vector2, Vector2>) ((x, y) => x + y))},
        {typeof(Vector3), (Vector3.Zero, (Func<Vector3, Vector3, Vector3>) ((x, y) => x + y))},
        {typeof(Vector4), (Vector4.Zero, (Func<Vector4, Vector4, Vector4>) ((x, y) => x + y))},
        {typeof(FColor), (new FColor(0, 0, 0, 0), (Func<FColor, FColor, FColor>) ((x, y) => x + y))},
    };
    private static readonly Dictionary<Type, object> multiplyOps = new() {
        {typeof(int), (Func<int, int, int>) ((x, y) => x * y)},
        {typeof(float), (Func<float, float, float>) ((x, y) => x * y)},
        {typeof(Vector2), (Func<Vector2, float, Vector2>) ((x, y) => x * y)},
        {typeof(Vector3), (Func<Vector3, float, Vector3>) ((x, y) => x * y)},
        {typeof(Vector4), (Func<Vector4, float, Vector4>) ((x, y) => x * y)},
        {typeof(FColor), (Func<FColor, float, FColor>) ((x, y) => x * y)},
    };
    private static readonly Dictionary<Type, object> vecMultiplyOps = new() {
        {typeof(int), (1, (Func<int, int, int>) ((x, y) => x * y))},
        {typeof(float), (1f, (Func<float, float, float>) ((x, y) => x * y))},
        {typeof(Vector2), (Vector2.One, (Func<Vector2, Vector2, Vector2>) ((x, y) => x * y))},
        {typeof(Vector3), (Vector3.One, (Func<Vector3, Vector3, Vector3>) ((x, y) => x * y))},
        {typeof(Vector4), (Vector4.One, (Func<Vector4, Vector4, Vector4>) ((x, y) => x * y))},
        {typeof(FColor), (new FColor(1, 1, 1, 1), (Func<FColor, FColor, FColor>) ((x, y) => x * y))},
    };
    public static Func<T, T, float, T> GetLerp<T>() => lerpers.TryGetValue(typeof(T), out var l) ?
        (Func<T, T, float, T>)l :
        throw new Exception($"No lerp handling for type {typeof(T)}");
    public static Func<T, float, T> GetMulOp<T>() => multiplyOps.TryGetValue(typeof(T), out var l) ?
        (Func<T, float, T>)l :
        throw new Exception($"No multiply handling for type {typeof(T)}");
    
    public static (T zero, Func<T, T, T> add) GetAddOp<T>() => addOps.TryGetValue(typeof(T), out var l) ?
        ((T, Func<T, T, T>))l :
        throw new Exception($"No add handling for type {typeof(T)}");
    
    public static (T zero, Func<T, T, T> add) GetVecMulOp<T>() => vecMultiplyOps.TryGetValue(typeof(T), out var l) ?
        ((T, Func<T, T, T>))l :
        throw new Exception($"No vec-multiply handling for type {typeof(T)}");

    /// <summary>
    /// Lerper should be unclamped.
    /// </summary>
    public static void RegisterLerper<T>(Func<T, T, float, T> lerper) => lerpers[typeof(T)] = lerper;
    public static void RegisterMultiplier<T>(Func<T, float, T> mulOp) => multiplyOps[typeof(T)] = mulOp;
    public static void RegisterAdder<T>((T, Func<T, T, T>) addOp) => addOps[typeof(T)] = addOp;
    public static void RegisterVecMultiplier<T>((T, Func<T, T, T>) vecMulOp) => 
        vecMultiplyOps[typeof(T)] = vecMulOp;

    public static void RegisterType<T>(Func<T, T, float, T> lerper, Func<T, float, T> mulOp, 
        (T, Func<T, T, T>) addOp, (T, Func<T, T, T>) vecMulOp) {
        RegisterLerper(lerper);
        RegisterMultiplier(mulOp);
        RegisterAdder(addOp);
        RegisterVecMultiplier(vecMulOp);
    }
}
}