namespace Suzunoya.ControlFlow {
public enum SkipMode {
    /// <summary>
    /// Loading state during backlogging or loading a save file.
    /// </summary>
    LOADING,
    /// <summary>
    /// Autoplay state (confirms after delay) that can be activated at player will.
    /// </summary>
    AUTOPLAY,
    /// <summary>
    /// Fast-forward state that can be activated at player will.
    /// </summary>
    FASTFORWARD
}

}