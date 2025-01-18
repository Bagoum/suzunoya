using System;
using System.Collections.Generic;
using System.Reactive;
using BagoumLib.Events;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor.Analysis;

/// <summary>
/// An environment frame, storing the local variables for an instantiated lexical scope,
/// in script code execution.
/// </summary>
[PublicAPI]
public class EnvFrame {
    /// <summary>
    /// An event for when envframe and framevar caches should be cleared.
    /// </summary>
    public static Event<Unit> ClearCache { get; } = new();
    /// <summary>
    /// Number of envframes that have been created (including from cache).
    /// </summary>
    public static int Created = 0;
    /// <summary>
    /// Number of envframes that have been cloned (including from cache).
    /// </summary>
    public static int Cloned = 0;
    /// <summary>
    /// Number of envframes that have been returned to cache.
    /// </summary>
    public static int Disposed = 0;
    private static readonly Stack<EnvFrame> cache = new();
    /// <summary>
    /// An empty envframe using the global lexical scope.
    /// </summary>
    public static EnvFrame Empty { get; } = new();
    private static uint counter;
    /// <summary>
    /// The counter id for this envframe.
    /// </summary>
    public uint Ctr { get; } = counter++;
    
    private int dependents = 0;
    private int owners = 0;
    /// <summary>
    /// The environment frame that contains this one.
    /// </summary>
    public EnvFrame? Parent { get; private set; }
    private void TakeParent(EnvFrame? parent) {
        Parent?.FreeDependent();
        if ((Parent = parent) != null)
            ++Parent.dependents;
    }

    /// <summary>
    /// The lexical scope for which this envframe is instantiated.
    /// </summary>
    public LexicalScope Scope { get; private set; } = GlobalScope.Singleton;
    
    /// <summary>
    /// The variables stored in this environment frame.
    /// </summary>
    public object[] Variables = null!;
    
    /// <summary>
    /// The float-typed frame variables in this environment frame
    ///  (also available in <see cref="Variables"/>).
    /// </summary>
    public float[]? FloatVars = null;

    /// <summary>
    /// Get the i'th parent of this envframe.
    /// </summary>
    public EnvFrame this[int parentage] {
        get {
            var ef = this;
            for (int ii = 0; ii < parentage; ++ii) {
                ef = ef!.Parent;
            }
            return ef!;
        }
    }
    
    private EnvFrame() { }

    /// <summary>
    /// Creates a new environment frame in the provided scope, as a child of the parent frame.
    /// <br/>If the provided scope disallows instantiating environment frames (<see cref="LexicalScope.UseEF"/> false),
    ///  or the provided scope is the same as the parent scope,
    ///  then transparently clones the parent frame instead.
    /// </summary>
    public static EnvFrame Create(LexicalScope scope, EnvFrame? parent) {
        //todo: handle crushed scopes
        if (scope is DynamicLexicalScope dyn) {
            scope = dyn.RealizeScope(parent?.Scope ??
                                     throw new Exception(
                                         "Dynamic lexical scopes must be instantiated with a parent envFrame"));
        }
        if (scope == parent?.Scope)
            return parent.Mirror();
        var np = scope.Parent;
        for (; np is { UseEF: false }; np = np.Parent) { }
        if ((parent?.Scope ?? scope.GlobalRoot) != np) {
            //Allow const scopes to have null parentage
            if (!(scope.IsConstScope && parent is null))
                throw new Exception("Incorrect envframe instantiation: parent scope is not the same as scope parent");
        }
        if (!scope.UseEF) 
            return (parent ?? throw new StaticException("Parent EF must be provided for non-EF scopes")).Mirror();
        var ef = cache.Count > 0 ? cache.Pop() : new();
        ef.TakeParent(parent);
        //Logs.Log($"CREATE {ef.Ctr} ({ef.Parent?.Ctr})", stackTrace: true);
        ++Created;
        ef.Scope = scope;
        ef.owners = 1;
        ef.Variables = EFArrayPool<object>.Rent(scope.VariableDecls.Length);
        for (int ii = 0; ii < scope.VariableDecls.Length; ++ii) {
            var (typ, decls) = scope.VariableDecls[ii];
            var v = IVariableStoreCreator.Create(typ, decls.Length);
            ef.Variables[ii] = v;
            if (v is float[] vf)
                ef.FloatVars = vf;
        }
        return ef;
    }
    
    internal static readonly ExFunction exCreate = ExFunction.WrapAny(typeof(EnvFrame), nameof(Create));
    
    /// <summary>
    /// Get a value stored in this envframe or a parent envframe.
    /// <br/>If the variable does not exist, return Maybe.None.
    /// If the variable exists but has the wrong type, throw an exception.
    /// </summary>
    public Maybe<T> MaybeGetValue<T>(string varName) {
        for (var envFrame = this; envFrame != null; envFrame = envFrame.Parent) {
            if (envFrame.Scope.variableDecls.TryGetValue(varName, out var decl)) {
                if (decl.FinalizedType != typeof(T))
                    throw new Exception(
                        $"Types do not align for variable {varName}. Requested: {typeof(T).SimpRName()}; found: {decl.FinalizedType?.SimpRName()}");
                return ((T[])envFrame.Variables[decl.TypeIndex])[decl.Index];
            }
        }
        return Maybe<T>.None;
    }
    
    /// <summary>
    /// Get a reference to a value stored in this envframe or a parent envframe.
    /// </summary>
    public ref T Value<T>(string varName) {
        for (var envFrame = this; envFrame != null; envFrame = envFrame.Parent) {
            if (envFrame.Scope.variableDecls.TryGetValue(varName, out var decl)) {
                if (decl.FinalizedType != typeof(T))
                    throw new Exception(
                        $"Types do not align for variable {varName}. Requested: {typeof(T).SimpRName()}; found: {decl.FinalizedType?.SimpRName()}");
                return ref ((T[])envFrame.Variables[decl.TypeIndex])[decl.Index];
            }
        }
        throw new Exception($"Variable {varName} not found in environment frame");
    }
    
    /// <summary>
    /// Get a reference to a value stored in this envframe or a parent envframe.
    /// </summary>
    public ref T Value<T>(VarDecl decl) {
        if (decl == null) throw new Exception($"Declaration not provided to {nameof(Value)}");
        for (var envFrame = this; envFrame != null; envFrame = envFrame.Parent) {
            if (decl.DeclarationScope == envFrame.Scope || decl.DeclarationScope == envFrame.Scope.DynRealizeSource)
                return ref ((T[])envFrame.Variables[decl.TypeIndex])[decl.Index];
        }
        throw new Exception($"Variable {decl.Name}<{decl.FinalizedType!.SimpRName()}> not found in environment frame");
    }

    /// <summary>
    /// Get a value stored in this envframe or a parent envframe.
    /// </summary>
    public T NonRefValue<T>(VarDecl decl) => Value<T>(decl);

    /// <summary>
    /// Get the local type variables for an envFrame.
    /// </summary>
    public static Ex FrameVarValues(Ex envFrame, Ex typIdx, Type typ) =>
        //(ef.Variables[var.TypIdx] as {var.Typ}[])
        (typ == typeof(float) ?
            envFrame.Field(nameof(FloatVars)) :
            envFrame
                .Field(nameof(Variables)).Index(typIdx)
                .As(typ.MakeArrayType()));
    
    /// <summary>
    /// Get the value of a local variable for an envFrame.
    /// </summary>
    public static Ex Value(Ex envFrame, Ex typeIdx, Ex valueIdx, Type typ) => 
        FrameVarValues(envFrame, typeIdx, typ).Index(valueIdx);

    /// <summary>
    /// Free this EnvFrame. It will only be returned to the cache when all dependents have freed it.
    /// </summary>
    public void Free() {
        if (this == Empty) return;
        //Logs.Log($"free {Ctr} ({owners - 1} rem)", stackTrace: true);
        if (--owners == 0 && dependents == 0)
            Dispose();
    }
    
    internal static readonly ExFunction exFree = ExFunction.WrapAny(typeof(EnvFrame), nameof(Free));


    private void FreeDependent() {
        if (this == Empty) return;
        if (--dependents == 0 && owners == 0) {
            Dispose();
        }
    }

    private void Dispose() {
        if (this == Empty) return;
        //Logs.Log($"DISPOSED {Ctr} ({Parent?.Ctr})", stackTrace: true);
        ++Disposed;
        FloatVars = null;
        for (int ii = 0; ii < Scope.VariableDecls.Length; ++ii)
            IVariableStoreCreator.CacheAny(Variables[ii]);
        EFArrayPool<object>.Return(Variables);
        Variables = null!;
        cache.Push(this);
        TakeParent(null); //calls FreeDependent
    }

    /// <summary>
    /// Return this envframe, but mark an additional owner such that the envframe will only be disposed
    ///  when both owners are finished using it.
    /// </summary>
    public EnvFrame Mirror() {
        if (this == Empty) return Empty;
        ++owners;
        //Logs.Log($"dup {Ctr} ({owners} rem)", stackTrace: true);
        return this;
    }
    
    /// <summary>
    /// Copy all the values inside this envframe into a new envframe. The parent frame is shared, but the
    ///  local values are not.
    /// </summary>
    public EnvFrame Clone() {
        if (this == Empty) return Empty;
        var nxt = cache.Count > 0 ? cache.Pop() : new();
        nxt.TakeParent(Parent);
        nxt.Scope = Scope;
        nxt.owners = 1;
        //Logs.Log($"CLONE {nxt.Ctr} <- {Ctr} ({Parent?.Ctr})", stackTrace: true);
        ++Cloned;
        nxt.Variables = EFArrayPool<object>.Rent(Scope.VariableDecls.Length);
        for (int ii = 0; ii < Scope.VariableDecls.Length; ++ii) {
            nxt.Variables[ii] = IVariableStoreCreator.CloneAny(Variables[ii]);
            if (nxt.Variables[ii] is float[] fv)
                nxt.FloatVars = fv;
        }
        return nxt;
    }
}

/// <summary>
/// Helper interface for creating typed variable arrays in <see cref="EnvFrame"/>.
/// </summary>
internal interface IVariableStoreCreator {
    private static readonly Dictionary<Type, IVariableStoreCreator> creators = new();
    
    /// <inheritdoc cref="Create(Type, int)"/>
    protected object Create(int len);

    /// <inheritdoc cref="CacheAny"/>
    protected void Cache(object data);
    
    /// <inheritdoc cref="CloneAny"/>
    protected object Clone(object data);

    /// <summary>
    /// Get an instance of T[].
    /// </summary>
    public static object Create(Type t, int len) {
        if (!creators.TryGetValue(t, out var c))
            creators[t] = c = Activator.CreateInstance(typeof(VariableStoreCreator<>).MakeGenericType(t)) 
                                  as IVariableStoreCreator ?? 
                              throw new Exception($"Failed to generate VariableStoreCreator for type {t.SimpRName()}");
        return c.Create(len);
    }
    
    /// <summary>
    /// Dispose of any array T[] by returning it to a type-specific cache.
    /// </summary>
    public static void CacheAny(object data) =>
        creators[data.GetType().GetElementType()!].Cache(data);
    
    /// <summary>
    /// Clone any array T[].
    /// </summary>
    public static object CloneAny(object data) => 
        creators[data.GetType().GetElementType()!].Clone(data);
}

/// <inheritdoc cref="IVariableStoreCreator"/>
internal class VariableStoreCreator<T> : IVariableStoreCreator {
    object IVariableStoreCreator.Create(int len) => EFArrayPool<T>.Rent(len, clear: true);

    /// <inheritdoc/>
    public void Cache(object data) {
        if (data is not T[] arr)
            throw new Exception(
                $"Frame data for caching should be of type {typeof(T[]).RName()} but was of type {data.GetType().RName()}");
        EFArrayPool<T>.Return(arr);
    }

    /// <inheritdoc/>
    public object Clone(object data) {
        if (data is not T[] arr)
            throw new Exception(
                $"Frame data for cloning should be of type {typeof(T[]).RName()} but was of type {data.GetType().RName()}");
        var cpy = EFArrayPool<T>.Rent(arr.Length);
        Array.Copy(arr, cpy, arr.Length);
        return cpy;
    }
}

//Implementation of ArrayPool of small lengths that isn't thread-safe but has zero amortized allocation
/// <summary>
/// Cache for storing arrays of small length.
/// </summary>
internal static class EFArrayPool<T> {
    //We have buckets as follows:
    //One bucket for each length up to 8.
    //From there, one bucket for each exponent [8+2^n, 8+2*2^n).
    private static readonly List<Queue<T[]>> buckets = new();

    private static void AssertBucket(int bucket) {
        while (buckets.Count <= bucket)
            buckets.Add(new());
    }

    internal static int BucketForGetLength(in int len) {
        if (len <= 8)
            return len - 1;
        var idx = 8;
        for (int diff = len - 8; diff > 1; diff /= 2)
            ++idx;
        //Unless the required length is the minimum in the bucket,
        // we need to go up one bucket to guarantee that the array is long enough.
        //eg. bucket 10 contains arrays of length [12,16).
        // if we need an array of length 14, we need to go up to bucket 11.
        return len == 8 + (1 << (idx - 8)) ? idx : idx + 1;
    }
    
    internal static int BucketForSetLength(in int len) {
        if (len <= 8)
            return len - 1;
        var idx = 8;
        for (int diff = len - 8; diff > 1; diff /= 2)
            ++idx;
        return idx;
    }

    /// <summary>
    /// Return an array to the cache.
    /// </summary>
    public static void Return(T[] array) {
        if (array.Length == 0) return;
        var bucket = BucketForSetLength(array.Length);
        AssertBucket(bucket);
        buckets[bucket].Enqueue(array);
    }

    /// <summary>
    /// Rent an array from the cache, and if required, clear its data to 0.
    /// </summary>
    public static T[] Rent(int len, bool clear = false) {
        if (len == 0) return [];
        var bucket = BucketForGetLength(len);
        for (; bucket < buckets.Count; ++bucket)
            if (buckets[bucket].TryDequeue(out var arr)) {
                if (clear)
                    Array.Clear(arr, 0, arr.Length);
                return arr;
            }
        return new T[len];
    }


    static EFArrayPool() => EnvFrame.ClearCache.Subscribe(_ => {
        foreach (var q in buckets)
            q.Clear();
        buckets.Clear();
    });
}
