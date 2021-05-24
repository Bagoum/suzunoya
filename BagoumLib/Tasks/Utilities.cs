﻿using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace BagoumLib.Tasks {
[PublicAPI]
public static class Utilities {
    private static Action<Task> WrapRethrow(Action cb) => t => {
        Exception? exc = t.Exception;
        try {
            cb();
        } catch (Exception e) {
            exc = new Exception(e.Message, exc);
        }
        if (exc != null) {
            Logging.Log(LogMessage.Error(exc, 
                "Exceptions occured within a task continuation. " +
                "If this continuation is awaited by the main thread, then this error may be repeated below."));
            throw exc;
        }
    };

    public static Task ContinueWithSync(this Task t, Action done) =>
        t.ContinueWith(WrapRethrow(done), TaskContinuationOptions.ExecuteSynchronously);
}
}