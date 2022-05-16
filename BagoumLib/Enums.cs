namespace BagoumLib {
/// <summary>
/// Enum describing the completion status of a task.
/// </summary>
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

/// <summary>
/// Enum describing the status of a process that may be interrupted by another process.
/// </summary>
public enum InterruptionStatus : int {
    /// <summary>
    /// The process has not yet been interrupted.
    /// </summary>
    Normal = 0,
    /// <summary>
    /// The process was previously interrupted but may continue.
    /// </summary>
    Continue = 1,
    /// <summary>
    /// The process is currently being interrupted by another process.
    /// </summary>
    Interrupted = 2,
    /// <summary>
    /// The process should abort due to a signal from an interrupting process.
    /// </summary>
    Abort = 3
}

}