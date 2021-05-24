﻿using System;
using System.Reactive.Subjects;
using System.Text;
using BagoumLib.Events;
using JetBrains.Annotations;

namespace BagoumLib {
[PublicAPI]
public enum LogLevel {
    DEBUG1 = 1,
    DEBUG2 = 2,
    DEBUG3 = 3,
    DEBUG4 = 4,
    INFO = 5,
    WARNING = 6,
    ERROR = 7
}
[PublicAPI]
public readonly struct LogMessage {
    public string Message { get; }
    public bool? ShowStackTrace { get; }
    public LogLevel Level { get; }
    public Exception? Exception { get; }

    public LogMessage(string message, LogLevel level, Exception? exception, bool? showStackTrace = null) {
        Message = message;
        ShowStackTrace = showStackTrace;
        Level = level;
        Exception = exception;
    }

    public static LogMessage Info(string message, LogLevel level = LogLevel.INFO) => 
        new(message, level, null);
    
    public static LogMessage Warning(string message) => 
        new(message, LogLevel.WARNING, null);
    
    public static LogMessage Error(Exception? exception, string message = "") => 
        new(message, LogLevel.ERROR, exception, true);
}

[PublicAPI]
public static class Logging {
    public static readonly ISubject<LogMessage> Logs = new PersistentEvent<LogMessage>();
    public static void Log(LogMessage lm) => Logs.OnNext(lm);

}
}