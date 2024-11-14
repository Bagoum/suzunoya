using System;

namespace Suzunoya.Data {
/// <summary>
/// Settings for VN execution.
/// </summary>
public interface ISettings {
    /// <summary>
    /// Text speed multiplier.
    /// </summary>
    float TextSpeed { get; }
}

/// <inheritdoc cref="ISettings"/>
[Serializable]
public class Settings : ISettings {
    /// <inheritdoc/>
    public float TextSpeed { get; set; } = 1f;
}
}