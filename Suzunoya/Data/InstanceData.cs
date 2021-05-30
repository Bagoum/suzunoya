using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Suzunoya.ControlFlow;

namespace Suzunoya.Data {

public interface IInstanceData : IKeyValueRepository {
    IGlobalData GlobalData { get; }
    List<(string scriptId, int line)> Location { get; }
    public void SaveVNLocation(IVNState vn);
}

[Serializable]
public class InstanceData : KeyValueRepository, IInstanceData {
    [field:NonSerialized] [JsonIgnore]
    public IGlobalData GlobalData { get; init; } = new GlobalData();
    public List<(string scriptId, int line)> Location { get; set; } = new();

    public void SaveVNLocation(IVNState vn) {
        var locs = new List<(string, int)>();
        foreach (var ctx in vn.ExecCtxes) {
            //Can't save the location if any script in the stack is unidentifiable
            if (string.IsNullOrEmpty(ctx.ScriptID))
                return;
            locs.Add((ctx.ScriptID!, ctx.Line));
        }
        Location = locs;
    }
}
}