using System;
using System.Text;
using JetBrains.Annotations;

namespace BagoumLib {
[PublicAPI]
public static class Exceptions {
    public static string PrintNestedException(Exception e) {
        StringBuilder msg = new();
        var lastStackTrace = e.StackTrace;
        Exception? exc = e;
        while (exc != null) {
            msg.Append(exc.Message);
            msg.Append("\n");
            lastStackTrace = exc.StackTrace;
            exc = exc.InnerException;
        }
        msg.Append("\n");
        msg.Append(lastStackTrace);
        return msg.ToString();
    }

    public static Exception FlattenNestedException(Exception e) => new Exception(PrintNestedException(e));
}
}