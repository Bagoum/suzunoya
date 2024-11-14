using System;
using BagoumLib;
using JetBrains.Annotations;
using Mizuhashi;

namespace Scriptor;

/// <summary>
/// An exception thrown due incorrect compile-time behavior.
/// </summary>
[PublicAPI]
public class StaticException : Exception {
    public StaticException(string message) : base($"STATIC ERROR: {message}") { }
}

/// <summary>
/// An exception thrown due to problems in reflection or script parsing.
/// </summary>
[PublicAPI]
public class ReflectionException : Exception {
    public PositionRange Position { get; }
    public PositionRange? HighlightedPosition { get; }
    public string MessageWithoutPosition { get; }

    public ReflectionException(PositionRange pos, string message, Exception? inner = null) : base($"{pos}: {message}", inner) {
        this.MessageWithoutPosition = message;
        this.Position = pos;
    }
    
    public ReflectionException(PositionRange pos, PositionRange highlighted, string message, Exception? inner = null) : 
        base($"{pos} ≪{highlighted}≫: {message}", inner) {
        this.MessageWithoutPosition = message;
        this.Position = pos;
        this.HighlightedPosition = highlighted;
    }

    public static ReflectionException Make(PositionRange pos, PositionRange? highlighted, string message,
        Exception? inner) =>
        highlighted.Try(out var hp) ?
            new ReflectionException(pos, hp, message, inner) :
            new ReflectionException(pos, message, inner);

    public ReflectionException Copy(Exception newInner) =>
        ReflectionException.Make(Position, HighlightedPosition, MessageWithoutPosition, newInner);
    
}

/// <summary>
/// An exception thrown when the type of an expression is incorrect.
/// </summary>
[PublicAPI]
public class BadTypeException : Exception {
    public BadTypeException(string message) : base(message) { }
    public BadTypeException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// An exception thrown due a failure in expression compilation.
/// </summary>
[PublicAPI]
public class CompileException : Exception {
    public CompileException(string message) : base(message) { }
    public CompileException(string message, Exception inner) : base(message, inner) { }
}


public class NotWriteableException : Exception {
    public int ArgIndex { get; }

    public NotWriteableException(int argIndex, string err) : base(err) {
        this.ArgIndex = argIndex;
    }

    public NotWriteableException(int argIndex, string err, Exception inner) : base(err) {
        this.ArgIndex = argIndex;
    }
}