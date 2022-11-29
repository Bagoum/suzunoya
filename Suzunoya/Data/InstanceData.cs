using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BagoumLib.Functional;
using Newtonsoft.Json;
using Suzunoya.ControlFlow;

namespace Suzunoya.Data {

/// <summary>
/// A recursive data structure containing locals and results for bounded contexts in the process
/// of execution or having finished execution.
/// The result value is typed in subclass <see cref="BoundedContextData{T}"/>.
/// </summary>
/// <param name="Key">String key for this bounded context. If empty, it cannot be accurately saved.</param>
/// <param name="Locals">Variables saved to this bounded context via <see cref="VNState.SaveLocalValue{T}"/>.</param>
/// <param name="Nested">Data for bounded contexts executed within this one.</param>
[Serializable]
public abstract record BoundedContextData(string Key, KeyValueRepository Locals,
    Dictionary<string, BoundedContextData> Nested) {
    /// <summary>
    /// Save the information of a bounded context executed while nested within this one.
    /// </summary>
    public void SaveNested(BoundedContextData data, bool allowOverride=false) => SaveNested(this, data, allowOverride);

    /// <summary>
    /// Check if there exists saved information for a nested bounded context with the provided key.
    /// </summary>
    public bool HasNested(string? key) => HasNested(this, key);

    /// <summary>
    /// Get the saved information for a nested bounded context with the provided key.
    /// </summary>
    public BoundedContextData GetNested(string key) => GetNested(this, key);
    
    /// <inheritdoc cref="GetNested"/>
    public BoundedContextData<T> GetNested<T>(string key) => GetNested<T>(this, key);

    /// <summary>
    /// Cast this to a more specific type.
    /// </summary>
    public BoundedContextData<T> CastTo<T>() =>
        this as BoundedContextData<T> ??
            throw new Exception($"Definition for BoundedContextData {Key} was not of type {typeof(T)}");
    
    internal static void SaveNested(Either<BoundedContextData, Dictionary<string, BoundedContextData>> parent, BoundedContextData data, bool allowOverride) {
        var key = data.Key;
        var nested = parent.IsLeft ? parent.Left.Nested : parent.Right;
        if (nested.ContainsKey(key) && !allowOverride && key != "")
            throw new Exception($"Duplicate definition for key {parent.LeftOrNull?.Key}->{key}");
        nested[key] = data;
    }

    internal static bool HasNested(Either<BoundedContextData, Dictionary<string, BoundedContextData>> parent,
        string? key) {
        var nested = parent.IsLeft ? parent.Left.Nested : parent.Right;
        return key != null && nested.ContainsKey(key);
    }
    
    internal static BoundedContextData GetNested(Either<BoundedContextData, Dictionary<string, BoundedContextData>> parent, string key) {
        var nested = parent.IsLeft ? parent.Left.Nested : parent.Right;
        if (!nested.TryGetValue(key, out var v))
            throw new Exception($"Could not find BCTXData for {parent.LeftOrNull?.Key}->{key}");
        return v;
    }
    
    internal static BoundedContextData<T> GetNested<T>(Either<BoundedContextData, Dictionary<string, BoundedContextData>> parent, string key) => GetNested(parent, key).CastTo<T>();
}

/// <summary>
/// See <see cref="BoundedContextData"/>
/// </summary>
[Serializable]
public record BoundedContextData<T>(string Key, Maybe<T> Result, KeyValueRepository Locals,
    Dictionary<string, BoundedContextData> Nested) : BoundedContextData(Key, Locals, Nested) {
    /// <summary>
    /// Result returned by the bounded context.
    /// <br/>This is None while the context is executing.
    /// </summary>
    public Maybe<T> Result { get; set; } = Result;
}

/// <summary>
/// Data for a (saveable) in-progress VN sequence.
/// </summary>
public interface IInstanceData {
    /// <summary>
    /// Global save data, shared between instances.
    /// </summary>
    IGlobalData GlobalData { get; }
    /// <summary>
    /// Position in any executing VN dialogue.
    /// </summary>
    VNLocation? Location { get; set; }
    
    /// <summary>
    /// Save the locals and result information from a top-level bounded context execution.
    /// </summary>
    void SaveBCtxData(BoundedContextData data, bool allowOverride=false);

    /// <summary>
    /// Check if there exists saved top-level bounded context information for the provided key.
    /// </summary>
    bool HasBCtxData(string? key);

    /// <summary>
    /// Get the locals and result information from a top-level bounded context with the provided key.
    /// </summary>
    BoundedContextData GetBCtxData(string key);
    
    /// <inheritdoc cref="GetBCtxData"/>
    BoundedContextData<T> GetBCtxData<T>(string key);

    /// <summary>
    /// Get the locals and result information from a bounded context that was executed while nested in
    ///  other bounded contexts.
    /// </summary>
    /// <param name="keys">Bounded context keys describing the nesting state, starting with the top-level and ending with the key for the bounded context that needs to be retrieved.</param>
    BoundedContextData? TryGetChainedData(params string[] keys) {
        if (keys.Length == 0)
            throw new Exception("Cannot get chained BCtxData for 0 keys");
        if (!HasBCtxData(keys[0]))
            return null;
        var bctx = GetBCtxData(keys[0]);
        for (int ii = 1; ii < keys.Length; ++ii)
            if (bctx.HasNested(keys[ii]))
                bctx = bctx.GetNested(keys[ii]);
            else
                return null;
        return bctx;
    }
    
    /// <inheritdoc cref="TryGetChainedData"/>
    BoundedContextData<T>? TryGetChainedData<T>(params string[] keys) => 
        TryGetChainedData(keys)?.CastTo<T>();

    /// <inheritdoc cref="TryGetChainedData"/>
    BoundedContextData<T>? TryGetChainedData<T>(IList<OpenedContext> ctxs) {
        if (ctxs.Count == 0)
            throw new Exception("Cannot get chained BCtxData for 0 contexts");
        if (!HasBCtxData(ctxs[0].ID))
            return null;
        var bctx = GetBCtxData(ctxs[0].ID);
        for (int ii = 1; ii < ctxs.Count; ++ii)
            if (bctx.HasNested(ctxs[ii].ID))
                bctx = bctx.GetNested(ctxs[ii].ID);
            else
                return null;
        return bctx.CastTo<T>();
    }
}

/// <summary>
/// A barebones implementation of <see cref="IInstanceData"/>.
/// </summary>
[Serializable]
public class InstanceData : IInstanceData {
    /// <summary>
    /// A frozen copy of the global data constructed when the instance data is initially created.
    /// </summary>
    public GlobalData FrozenGlobalData { get; init; }
    
    /// <inheritdoc cref="IInstanceData.GlobalData"/>
    [field:NonSerialized] [JsonIgnore]
    public GlobalData GlobalData { get; private set; }
    IGlobalData IInstanceData.GlobalData => GlobalData;
    /// <inheritdoc/>
    public VNLocation? Location { get; set; } = null;
    /// <summary>
    /// Top-level saved bounded context information.
    /// </summary>
    public Dictionary<string, BoundedContextData> BCtxData { get; init; } = new();

    /// <summary>
    /// Json constructor, do not use.
    /// </summary>
    [Obsolete]
#pragma warning disable 8618
    private InstanceData() { }
#pragma warning restore 8618
    
    /// <summary>
    /// </summary>
    /// <param name="global">A global data object. The constructor will freeze this object (ie. a deep copy)
    /// and provide it as <see cref="FrozenGlobalData"/>,
    /// and also provide a direct link as <see cref="GlobalData"/>.</param>
    public InstanceData(GlobalData global) {
        FrozenGlobalData = Serialization.DeserializeJson<GlobalData>(Serialization.SerializeJson(global))!;
        GlobalData = global;
    }

    /// <summary>
    /// Set the global data.
    /// </summary>
    /// <param name="g"></param>
    public void _SetGlobalData_OnlyUseForInitialization(GlobalData g) {
        GlobalData = g;
    }
    
    /// <inheritdoc/>
    public void SaveBCtxData(BoundedContextData data, bool allowOverride=false) => 
        BoundedContextData.SaveNested(BCtxData, data, allowOverride);
    /// <inheritdoc/>
    public bool HasBCtxData(string? key) => BoundedContextData.HasNested(BCtxData, key);
    
    /// <inheritdoc/>
    public BoundedContextData GetBCtxData(string key) => BoundedContextData.GetNested(BCtxData, key);
    /// <inheritdoc/>
    public BoundedContextData<T> GetBCtxData<T>(string key) => BoundedContextData.GetNested<T>(BCtxData, key);

    /// <summary>
    /// Recreate an <see cref="InstanceData"/> object from a JSON string.
    /// Will throw an exception if deserialization fails.
    /// </summary>
    /// <param name="serialized">The serialized JSON string.</param>
    /// <param name="currentGlobal">The current global data information. This will be linked as
    /// <see cref="GlobalData"/>, while <see cref="FrozenGlobalData"/> will be read from the JSON.</param>
    /// <returns></returns>
    public static T Deserialize<T>(string serialized, GlobalData currentGlobal) where T : InstanceData {
        var id = Serialization.DeserializeJson<T>(serialized) ?? 
                 throw new Exception("Deserialization returned a null InstanceData");
        id.GlobalData = currentGlobal;
        return id;
    }
}
}