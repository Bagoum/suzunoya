namespace BagoumLib {
public enum Completion : int {
    /// <summary>
    /// Process ran to completion.
    /// </summary>
    Standard = 0,
    /// <summary>
    /// Process received a skip signal. Will not throw OperationCancelledException.
    /// </summary>
    SoftSkip = 1,
    /// <summary>
    /// Process received a cancel signal. Will throw OperationCancelledException.
    /// </summary>
    Cancelled = 2
}
}