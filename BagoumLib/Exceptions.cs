using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace BagoumLib {
/// <summary>
/// An exception class with basically the same functionality as <see cref="AggregateException"/>, but with
/// more explicit control over the message.
/// </summary>
[PublicAPI]
public class MultiException : Exception {
    public List<Exception> InnerExceptions { get; }

    public MultiException(string message, IEnumerable<Exception> innerExcs) : base(message) {
        this.InnerExceptions = innerExcs.ToList();
    }
    public MultiException(string message, params Exception[] innerExcs) : this(message, innerExcs as IEnumerable<Exception>) { }
}

[PublicAPI]
public static class Exceptions {
    public record ExceptionMessagePrinter(StringBuilder Sb, int Indent = 0) {
        public void Print(ExceptionMessage error) {
            if (error is ExceptionMessage.Str s)
                Sb.Append(s.Message);
            else if (error is ExceptionMessage.Aggregate agg) {
                var indented = this with { Indent = Indent + 2 };
                Sb.Append(agg.Header);
                for (int ii = 0; ii < agg.Children.Length; ++ii) {
                    if (ii > 0)
                        AddNewline();
                    AddNewline();
                    Sb.Append($"Aggregate exception #{ii+1}/{agg.Children.Length}:");
                    indented.AddNewline();
                    indented.Print(agg.Children[ii]);
                }
            } else throw new ArgumentOutOfRangeException();
        }
        public virtual void Print(List<ExceptionMessage> errors) {
            for (int ii = 0; ii < errors.Count; ++ii) {
                if (ii > 0)
                    AddNewline();
                Print(errors[ii]);
            }
        }

        protected void AddNewline() {
            Sb.Append('\n');
            Sb.Append(MakeIndent());
        }
        private string MakeIndent() => Indent == 0 ? "" :  new string(' ', Indent);
    }
    
    public record InvertedExceptionMessagePrinter(StringBuilder Sb, int Indent = 0) : ExceptionMessagePrinter(Sb, Indent) {
        public override void Print(List<ExceptionMessage> errors) {
            for (int ii = 1; ii <= errors.Count; ++ii) {
                Print(errors[^ii]);
                if (ii < errors.Count) {
                    AddNewline();
                    if (ii == 1) //Break after innermost error
                        AddNewline();
                }
            }
        }
    }
    public abstract record ExceptionMessage {
        public record Str(string Message) : ExceptionMessage;
        public record Aggregate(string Header, List<ExceptionMessage>[] Children) : ExceptionMessage;

        public static implicit operator ExceptionMessage(string s) => new Str(s);
    }

    public static Exception? MaybeAggregate<T>(IList<T> errs) where T: Exception => errs.Count switch {
        0 => null,
        1 => errs[0],
        _ => new MultiException($"Found {errs.Count} errors.", errs)
    };

    public static (List<ExceptionMessage> messages, string? innermostStackTrace)
        GetNestedExceptionMessages(this Exception e) {
        var msgs = new List<ExceptionMessage>();
        var lastStackTrace = e.StackTrace;
        for (Exception? exc = e; exc != null; exc = exc.InnerException) {
            lastStackTrace = exc.StackTrace ?? lastStackTrace;
            if (exc is MultiException mul) {
                msgs.Add(new ExceptionMessage.Aggregate(mul.Message, 
                    mul.InnerExceptions.Select(ie => GetNestedExceptionMessages(ie).messages).ToArray()));
                break;
            } else if (exc is AggregateException agg) {
                msgs.Add(new ExceptionMessage.Aggregate(agg.Message, 
                    agg.InnerExceptions.Select(ie => GetNestedExceptionMessages(ie).messages).ToArray()));
                break;
            }
            msgs.Add(exc.Message);
        }
        return (msgs, lastStackTrace);
    }
    public static string PrintNestedException(Exception e, bool showStacktrace = true) {
        var sb = new StringBuilder();
        var (msgs, st) = GetNestedExceptionMessages(e);
        new ExceptionMessagePrinter(sb).Print(msgs);
        if (showStacktrace) {
            sb.Append("\n\n");
            sb.Append(st ?? "No stacktrace");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Return a string describing a nested exception, showing the innermost
    ///  exception first and then moving outwards.
    /// <br/>Includes a stacktrace from the innermost exception.
    /// </summary>
    public static string PrintNestedExceptionInverted(Exception e, bool showStacktrace = true) {
        var sb = new StringBuilder();
        var (msgs, st) = GetNestedExceptionMessages(e);
        new InvertedExceptionMessagePrinter(sb).Print(msgs);
        if (showStacktrace) {
            sb.Append("\n\n");
            sb.Append(st ?? "No stacktrace");
        }
        return sb.ToString();
    }
}
}