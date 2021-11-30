using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Suzunoya.ControlFlow;

namespace Suzunoya.Data {

public interface IInstanceData : IKeyValueRepository {
    IGlobalData GlobalData { get; }
    VNLocation? Location { get; set; }
}

/// <summary>
/// A barebones implementation of IInstanceData.
/// </summary>
[Serializable]
public class InstanceData : KeyValueRepository, IInstanceData {
    /// <summary>
    /// In order to preserve correctness across save/loads, it is generally beneficial to
    /// make global switches dependent on an unchanging version of the global data-- specifically, a frozen copy
    /// of the global data constructed on initialization.
    /// </summary>
    public GlobalData FrozenGlobalData { get; init; }
    [field:NonSerialized] [JsonIgnore]
    public GlobalData GlobalData { get; private set; }
    IGlobalData IInstanceData.GlobalData => GlobalData;
    public VNLocation? Location { get; set; } = null;

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
    /// Recreate an InstanceData object from a JSON string.
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