using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Suzunoya.ControlFlow;

namespace Suzunoya.Data {

public interface IGlobalData {
    ISettings Settings { get; }
    
    /// <summary>
    /// Inform the save data that a certain line has been read.
    /// </summary>
    void LineRead(string line);

    /// <summary>
    /// Check whether a line has been read.
    /// </summary>
    bool IsLineRead(string line);

    void GalleryCGViewed(string key);

    IReadOnlyCollection<string> Gallery { get; }
}

/// <summary>
/// A barebones implementation of IGlobalData.
/// </summary>
[Serializable]
public class GlobalData : IGlobalData {
    public Settings Settings { get; init; } = new();
    public HashSet<string> ReadLines { get; init; } = new();

    public HashSet<string> Gallery { get; init; } = new();
    
    [JsonIgnore] ISettings IGlobalData.Settings => Settings;
    [JsonIgnore] IReadOnlyCollection<string> IGlobalData.Gallery => Gallery;

    public void LineRead(string line) {
        ReadLines.Add(line);
    }

    public bool IsLineRead(string line) => ReadLines.Contains(line);

    public void GalleryCGViewed(string key) {
        Gallery.Add(key);
    }
}
}