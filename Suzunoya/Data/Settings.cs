using System;

namespace Suzunoya.Data {
public interface ISettings {
    float TextSpeed { get; }
}
[Serializable]
public class Settings : ISettings {
    public float TextSpeed { get; set; } = 1f;
}
}