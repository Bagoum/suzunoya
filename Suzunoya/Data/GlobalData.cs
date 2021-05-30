using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Suzunoya.Data {

public interface IGlobalData {
    ISettings Settings { get; }
    
    /// <summary>
    /// Inform the save data that a certain script line has been read.
    /// Noop if the script is null.
    /// </summary>
    void LineRead(string? scriptId, int line);
    /// <summary>
    /// Get the highest line number read within the given script.
    /// Returns null if the script is null and 0 if it has never been read.
    /// </summary>
    int? LastReadLine(string? scriptId);

    void GalleryCGViewed(string key);

    IReadOnlyCollection<string> Gallery { get; }
}

[Serializable]
public class GlobalData : IGlobalData {
    public Settings Settings { get; init; } = new();
    public Dictionary<string, int> ReadLines { get; init; } = new();

    public HashSet<string> Gallery { get; init; } = new();
    
    [JsonIgnore] ISettings IGlobalData.Settings => Settings;
    [JsonIgnore] IReadOnlyCollection<string> IGlobalData.Gallery => Gallery;

    public void LineRead(string? scriptId, int line) {
        if (string.IsNullOrEmpty(scriptId)) return;
        var existing = LastReadLine(scriptId) ?? 0;
        ReadLines[scriptId!] = Math.Max(existing, line);
    }

    public int? LastReadLine(string? scriptId) {
        if (string.IsNullOrEmpty(scriptId)) return null;
        return ReadLines.TryGetValue(scriptId!, out var l) ? l : null;
    }

    public void GalleryCGViewed(string key) {
        Gallery.Add(key);
    }
}
}