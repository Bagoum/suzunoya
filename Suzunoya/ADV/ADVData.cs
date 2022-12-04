using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Events;
using Newtonsoft.Json;
using Suzunoya.ControlFlow;
using Suzunoya.Data;

namespace Suzunoya.ADV {

/// <summary>
/// All data for a (saveable) in-progress ADV game instance.
/// <br/>This class should be derived for game-specific data.
/// </summary>
[Serializable]
public record ADVData(InstanceData VNData) {
    /// <summary>
    /// The currently realized map.
    /// </summary>
    public string CurrentMap { get; set; } = "";

    /// <summary>
    /// While in a VN segment, put the serialized save data before entering the segment here.
    /// </summary>
    public string? UnmodifiedSaveData { get; private set; } = null;

    /// <summary>
    /// When entering a context where save/load is not allowed (<see cref="StrongBoundedContext{T}.LoadSafe"/> is false),
    ///  put the save data at that point in time in this field.
    /// </summary>
    [JsonIgnore] public (List<string> ParentContexts, string Data)? LockedContextData { get; private set; } = null;
    
    //Json usage
    [Obsolete]
    public ADVData() : this(default(InstanceData)!) {}

    /// <summary>
    /// If this data was saved while in a VN segment (ie. UnmodifiedSaveData is not null),
    ///  then use UnmodifiedSaveData as the replayee save data, and use this data (or locked save data) as a loading proxy.
    /// </summary>
    public (ADVData main, ADVData? loadProxy) GetLoadProxyInfo() {
        if (UnmodifiedSaveData != null) {
            if (VNData.Location is null) {
                Logging.Log(new("Load proxy info was stored without a VNLocation. Please report this.", level: LogLevel.WARNING));
                return (this, null);
            }
            return (GetUnmodifiedSaveData()!, this);
        }
        return (this, null);
    }

    private static ADVData Deserialize(string data) {
        var save = Serialization.DeserializeJson<ADVData>(data) ?? 
                   throw new Exception($"Couldn't deserialize ADV data");
        save.VNData._SetGlobalData_OnlyUseForInitialization(ServiceLocator.Find<IGlobalVNDataProvider>().GlobalVNData);
        return save;
    }
    
    public ADVData? GetUnmodifiedSaveData() => 
        UnmodifiedSaveData is null ? null : Deserialize(UnmodifiedSaveData);

    public ADVData? GetLockedSaveData() =>
        LockedContextData?.Data is { } data ? Deserialize(data) : null;

    /// <summary>
    /// When entering a top-level bounded context,
    /// call this method to store the save data at that point in <see cref="UnmodifiedSaveData"/>.
    /// <br/>When loading into this bounded context, the engine will start with <see cref="UnmodifiedSaveData"/>
    ///  and replay it until it is equal to the savedata at the point of saving.
    /// </summary>
    public void PreserveData() {
        UnmodifiedSaveData = Serialization.SerializeJson(this, Formatting.None);
    }

    /// <summary>
    /// When leaving a top-level bounded context,
    /// call this method to remove the handling from <see cref="PreserveData"/>.
    /// </summary>
    public void RemovePreservedData() {
        UnmodifiedSaveData = null;
    }

    private static List<string> ContextKeys(OpenedContext bctx) => bctx.BCtx.VN.Contexts.Select(x => x.ID).ToList();
    
    /// <summary>
    /// When entering a bounded context that is not safe for save/load (<see cref="StrongBoundedContext{T}.LoadSafe"/>),
    /// call this method to store the save data at that point in <see cref="LockedContextData"/>.
    /// <br/><see cref="ADVManager.GetSaveReadyADVData"/> will return <see cref="LockedContextData"/> if it exists.
    /// </summary>
    public void LockContext(OpenedContext bctx) {
        //only the uppermost context is respected
        if (LockedContextData == null) {
            bctx.BCtx.VN.UpdateInstanceData();
            var keys = ContextKeys(bctx);
            Logging.Log($"Locking save data for keys {string.Join("::", keys)} and locked context {bctx.ID}");
            LockedContextData = (keys, Serialization.SerializeJson(this, Formatting.None));
        }
    }

    /// <summary>
    /// When leaving a bounded context that is not safe for save/load (<see cref="StrongBoundedContext{T}.LoadSafe"/>),
    /// call this method to remove the handling from <see cref="LockContext"/>.
    /// </summary>
    public void UnlockContext(OpenedContext bctx) {
        var keys = ContextKeys(bctx);
        if (LockedContextData?.ParentContexts is { } ctxs && ctxs.AreSame(keys)) {
            Logging.Log($"Unlocking save data for keys {string.Join("::", keys)} and locked context {bctx.ID}");
            LockedContextData = null;
        }
    }
}


}