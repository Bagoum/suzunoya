namespace Suzunoya.ControlFlow {
/// <summary>
/// An enum describing skip modes in Suzunoya VN control flow.
/// Note that "skipping not active" is not a member of this enum;
///  use SkipMode? if that is required.
/// </summary>
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