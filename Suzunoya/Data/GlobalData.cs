using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Suzunoya.ControlFlow;

namespace Suzunoya.Data {

/// <summary>
/// Container for data relevant to VN execution that is shared by all instances.
/// </summary>
public interface IGlobalData {
    /// <inheritdoc cref="ISettings"/>
    ISettings Settings { get; }
    
    /// <summary>
    /// Inform the save data that a certain line has been read.
    /// </summary>
    void LineRead(string line);

    /// <summary>
    /// Check whether a line has been read.
    /// </summary>
    bool IsLineRead(string line);
}

/// <summary>
/// A barebones implementation of <see cref="IGlobalData"/>.
/// </summary>
[Serializable]
public class GlobalData : IGlobalData {
    /// <inheritdoc cref="IGlobalData.Settings"/>
    public Settings Settings { get; init; } = new();
    /// <summary>
    /// The IDs of all executed VN lines.
    /// </summary>
    public HashSet<string> ReadLines { get; init; } = new();
    
    [JsonIgnore] ISettings IGlobalData.Settings => Settings;

    /// <inheritdoc/>
    public void LineRead(string line) {
        ReadLines.Add(line);
    }

    /// <inheritdoc/>
    public bool IsLineRead(string line) => ReadLines.Contains(line);
}

/// <summary>
/// A service providing an instance of <see cref="GlobalVNData"/> for VN initialization.
/// </summary>
public interface IGlobalVNDataProvider {
    /// <summary>
    /// Global instance of <see cref="GlobalVNData"/>.
    /// </summary>
    public GlobalData GlobalVNData { get; }
}

}