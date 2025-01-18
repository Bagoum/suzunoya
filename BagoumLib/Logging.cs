using System;
using System.Reactive.Subjects;
using System.Text;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using JetBrains.Annotations;

namespace BagoumLib {
/// <summary>
/// Logging levels for <see cref="Logging"/>.
/// </summary>
[PublicAPI]
public enum LogLevel {
    /// <summary>
    /// Lowest-priority debug level
    /// </summary>
    DEBUG1 = 1,
    /// <summary>
    /// Second-lowest-priority debug level
    /// </summary>
    DEBUG2 = 2,
    /// <summary>
    /// Second-highest-priority debug level
    /// </summary>
    DEBUG3 = 3,
    /// <summary>
    /// Highest-priority debug level
    /// </summary>
    DEBUG4 = 4,
    /// <summary>
    /// Informational logs (default level)
    /// </summary>
    INFO = 5,
    /// <summary>
    /// Warnings
    /// </summary>
    WARNING = 6,
    /// <summary>
    /// Errors/exceptions
    /// </summary>
    ERROR = 7
}
/// <summary>
/// A compiled log message that can be read by log listeners.
/// </summary>
[PublicAPI]
public readonly struct LogMessage {
    /// <summary>
    /// The log message.
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Whether or not the stack trace should be shown (null if the listener should determine automatically).
    /// </summary>
    public bool? ShowStackTrace { get; }
    
    /// <summary>
    /// The severity of the message.
    /// </summary>
    public LogLevel Level { get; }
    
    /// <summary>
    /// An exception that may be associated with the message.
    /// </summary>
    public Exception? Exception { get; }

    /// <inheritdoc cref="LogMessage"/>
    public LogMessage(string message, LogLevel level, Exception? exception = null, bool? showStackTrace = null) {
        Message = message;
        ShowStackTrace = showStackTrace;
        Level = level;
        Exception = exception;
    }

    /// <summary>
    /// Create a <see cref="LogMessage"/> with a default level of <see cref="LogLevel.INFO"/>.
    /// </summary>
    public static LogMessage Info(string message, LogLevel level = LogLevel.INFO) => 
        new(message, level);
    
    /// <summary>
    /// Create a warning log.
    /// </summary>
    public static LogMessage Warning(string message) => 
        new(message, LogLevel.WARNING);
    
    /// <summary>
    /// Create an error log.
    /// </summary>
    public static LogMessage Error(Exception? exception, string message = "") => 
        new(message, LogLevel.ERROR, exception, true);

    /// <summary>
    /// Convert a string to an info log.
    /// </summary>
    public static implicit operator LogMessage(string message) => LogMessage.Info(message);
    
    /// <summary>
    /// Convert an exception to an error log.
    /// </summary>
    public static implicit operator LogMessage(Exception e) => LogMessage.Error(e);

    /// <inheritdoc/>
    public override string ToString() => $"{Level}:{Message}";
}

/// <summary>
/// A static class containing a singleton logger.
/// </summary>
[PublicAPI]
public static class Logging {
    /// <summary>
    /// Singleton logger.
    /// </summary>
    public static readonly Logger Logs = new();
    
    /// <summary>
    /// Create a file link (clickable in some IDEs) linking to a line in a specific file
    /// </summary>
    /// <param name="filename">Path to file</param>
    /// <param name="line">Line in file</param>
    /// <param name="content">Message to put in the link text (defaults to filename:line)</param>
    /// <returns></returns>
    public static string ToFileLink(string? filename, int line, string? content = null) =>
        $"<a href=\"{filename}\" line=\"{line}\">{content ?? $"{filename}:{line}"}</a>";
}

/// <summary>
/// Helper struct for printing format strings in logging.
/// </summary>
public readonly struct LogStringFmt {
    private readonly string messageFmt;
    private readonly object[] args;
    /// <inheritdoc cref="LogStringFmt"/>
    public LogStringFmt(string messageFmt, params object[] args) {
        this.messageFmt = messageFmt;
        this.args = args;
    }

    /// <summary>
    /// Use string.Format to construct an output string.
    /// </summary>
    public string Realize() => string.Format(messageFmt, args);
}

/// <summary>
/// An entity that receives logging messages and dispatches them to multiple <see cref="ILogListener"/>s.
/// </summary>
[PublicAPI]
public class Logger : ILogListener {
    private readonly DMCompactingArray<ILogListener> listeners = new();
    private readonly Event<LogMessage> dispatcher = new();

    /// <inheritdoc/>
    public bool CanSkipMessage(LogLevel level, Exception? exc = null) {
        for (int ii = 0; ii < listeners.Count; ++ii)
            if (listeners.GetIfExistsAt(ii, out var l))
                if (!l.CanSkipMessage(level, exc))
                    return false;
        return true;
    }

    void IObserver<LogMessage>.OnCompleted() => dispatcher.OnCompleted();

    void IObserver<LogMessage>.OnError(Exception error) => dispatcher.OnError(error);

    void IObserver<LogMessage>.OnNext(LogMessage value) => dispatcher.OnNext(value);

    /// <summary>
    /// Register a listener to receive messages from this logger through the <see cref="IObserver{T}"/> interface.
    /// </summary>
    public IDisposable RegisterListener(ILogListener listener) {
        return new JointDisposable(null, listeners.Add(listener), dispatcher.Subscribe(listener));
    }
    
    /// <summary>
    /// Log a message.
    /// </summary>
    public void Log(string message, LogLevel level = LogLevel.INFO, bool? stacktrace = null) {
        dispatcher.OnNext(new(message, level, null, stacktrace));
    }

    /// <summary>
    /// Log a string-format message in one argument.
    /// </summary>
    public void Log(string messageFmt, object arg0, LogLevel level = LogLevel.INFO, bool? stacktrace = null) {
        if (!CanSkipMessage(level))
            dispatcher.OnNext(new(string.Format(messageFmt, arg0), level, null, stacktrace));
    }
    

    /// <summary>
    /// Log a string-format message in two arguments.
    /// </summary>
    public void Log(string messageFmt, object arg0, object arg1, LogLevel level = LogLevel.INFO, bool? stacktrace = null) {
        if (!CanSkipMessage(level))
            dispatcher.OnNext(new(string.Format(messageFmt, arg0, arg1), level, null, stacktrace));
    }

    /// <summary>
    /// Log a string-format message in three arguments.
    /// </summary>
    public void Log(string messageFmt, object arg0, object arg1, object arg2, LogLevel level = LogLevel.INFO, bool? stacktrace = null) {
        if (!CanSkipMessage(level))
            dispatcher.OnNext(new(string.Format(messageFmt, arg0, arg1, arg2), level, null, stacktrace));
    }

    /// <summary>
    /// Log a string-format message in many arguments.
    /// </summary>
    public void Log(LogStringFmt message, LogLevel level = LogLevel.INFO, bool? stacktrace = null) {
        if (!CanSkipMessage(level))
            dispatcher.OnNext(new(message.Realize(), level, null, stacktrace));
    }
    
    /// <summary>
    /// Log a warning.
    /// </summary>
    public void Warning(string message, bool? stacktrace = true) {
        if (!CanSkipMessage(LogLevel.WARNING))
            dispatcher.OnNext(new(message, LogLevel.WARNING, null, stacktrace));
    }

    /// <summary>
    /// Log an error.
    /// </summary>
    public void Error(Exception? exception, string message = "") {
        if (!CanSkipMessage(LogLevel.ERROR, exception))
            dispatcher.OnNext(new(message, LogLevel.ERROR, exception, true));
    }
}


/// <summary>
/// An entity that listens to published logging messages.
/// </summary>
public interface ILogListener : IObserver<LogMessage> {
    /// <summary>
    /// Returns true iff a message of a specified severity and exception will be no-oped by this listener.
    /// </summary>
    bool CanSkipMessage(LogLevel level, Exception? exc);
}

/// <summary>
/// A log listener that acts on any log messages, similar to using a callback as an IObserver.
/// </summary>
public record TrivialLogListener(Action<LogMessage> Callback) : ILogListener {
    /// <inheritdoc/>
    public void OnCompleted() {}

    /// <inheritdoc/>
    public void OnError(Exception error) {}

    /// <inheritdoc/>
    public void OnNext(LogMessage value) {
        Callback(value);
    }
    /// <inheritdoc/>
    public bool CanSkipMessage(LogLevel level, Exception? exc) => false;
}


}