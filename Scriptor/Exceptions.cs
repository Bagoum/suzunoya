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
    /// <inheritdoc cref="StaticException"/>
    public StaticException(string message) : base($"STATIC ERROR: {message}") { }
}

/// <summary>
/// An exception thrown due to problems in reflection or script parsing.
/// </summary>
[PublicAPI]
public class ReflectionException : Exception {
    /// <summary>
    /// Position in the source code causing the exception.
    /// </summary>
    public PositionRange Position { get; }
    /// <summary>
    /// A highlighted subrange of <see cref="Position"/>.
    /// </summary>
    public PositionRange? HighlightedPosition { get; }
    /// <summary>
    /// The exception message, not including the position.
    /// </summary>
    public string MessageWithoutPosition { get; }

    /// <inheritdoc cref="ReflectionException"/>
    public ReflectionException(PositionRange pos, string message, Exception? inner = null) : base($"{pos}: {message}", inner) {
        this.MessageWithoutPosition = message;
        this.Position = pos;
    }
    
    /// <inheritdoc cref="ReflectionException"/>
    public ReflectionException(PositionRange pos, PositionRange highlighted, string message, Exception? inner = null) : 
        base($"{pos} ≪{highlighted}≫: {message}", inner) {
        this.MessageWithoutPosition = message;
        this.Position = pos;
        this.HighlightedPosition = highlighted;
    }

    /// <inheritdoc cref="ReflectionException"/>
    public static ReflectionException Make(PositionRange pos, PositionRange? highlighted, string message,
        Exception? inner) =>
        highlighted.Try(out var hp) ?
            new ReflectionException(pos, hp, message, inner) :
            new ReflectionException(pos, message, inner);

    /// <summary>
    /// Duplicate this exception.
    /// </summary>
    public ReflectionException Copy(Exception newInner) =>
        ReflectionException.Make(Position, HighlightedPosition, MessageWithoutPosition, newInner);
    
}

/// <summary>
/// An exception thrown when the type of an expression is incorrect.
/// </summary>
[PublicAPI]
public class BadTypeException : Exception {
    /// <inheritdoc cref="BadTypeException"/>
    public BadTypeException(string message) : base(message) { }
    /// <inheritdoc cref="BadTypeException"/>
    public BadTypeException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// An exception thrown due a failure in expression compilation.
/// </summary>
[PublicAPI]
public class CompileException : Exception {
    /// <inheritdoc cref="CompileException"/>
    public CompileException(string message) : base(message) { }
    /// <inheritdoc cref="CompileException"/>
    public CompileException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// An exception thrown when script code attempts to write to a nonwriteable expression.
/// </summary>
public class NotWriteableException : Exception {
    /// <summary>
    /// Index of the argument in the relevant context.
    /// </summary>
    public int ArgIndex { get; }

    /// <inheritdoc cref="NotWriteableException"/>
    public NotWriteableException(int argIndex, string err) : base(err) {
        this.ArgIndex = argIndex;
    }

    /// <inheritdoc cref="NotWriteableException"/>
    public NotWriteableException(int argIndex, string err, Exception inner) : base(err, inner) {
        this.ArgIndex = argIndex;
    }
}