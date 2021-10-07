using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Suzunoya.ControlFlow;

namespace Suzunoya.Data {

public interface IInstanceData : IKeyValueRepository {
    IGlobalData GlobalData { get; }
    VNLocation? Location { get; set; }
}

[Serializable]
public class InstanceData : KeyValueRepository, IInstanceData {
    [field:NonSerialized] [JsonIgnore]
    public IGlobalData GlobalData { get; set; } = new GlobalData();
    public VNLocation? Location { get; set; } = null;
}
}