using System;
using BagoumLib;
using BagoumLib.Functional;

namespace Mizuhashi {
public readonly struct Position {
    /// <summary>
    /// Index in the soruce string (0-indexed) of the next character.
    /// <br/>This points out of bounds when the source has no elements left.
    /// </summary>
    public int Index { get; }
    /// <summary>
    /// Current line number (1-indexed).
    /// </summary>
    public int Line { get; }
    /// <summary>
    /// Index in the parse string of the first character in the current line.
    /// <br/>Note that newlines (\n) are considered to be "at the end of the previous line".
    /// <br/>Note that this may point out of bounds if the source ends with a newline.
    /// </summary>
    public int IndexOfLineStart { get; }
    /// <summary>
    /// Column of the next character (1-indexed). This does not handle tab width.
    /// </summary>
    public int Column => Index - IndexOfLineStart + 1;

    private (int, int, int) Tuple => (Index, Line, IndexOfLineStart);
    
    public Position(int index, int line, int indexOfLineStart) {
        Index = index;
        Line = line;
        IndexOfLineStart = indexOfLineStart;
    }

    public override string ToString() => $"Line {Line}, Col {Column}";

    public bool Equals(Position other) => this == other;
    public override bool Equals(object? obj) => obj is Position other && Equals(other);
    public override int GetHashCode() => Tuple.GetHashCode();
    public static bool operator ==(Position a, Position b) => a.Tuple == b.Tuple;
    public static bool operator !=(Position a, Position b) => !(a == b);
}
/// <summary>
/// A lightweight description of a parseable string.
/// </summary>
public readonly struct InputStream<S> {
    /// <summary>
    /// Source string.
    /// </summary>
    public string Source { get; }
    /// <summary>
    /// User-defined state variable.
    /// </summary>
    public S State { get; }
    public Position Position { get; }
    public int Index => Position.Index;
    /// <summary>
    /// Human-readable description of this parseable stream.
    /// </summary>
    public string Description { get; }

    public int Remaining => Source.Length - Index;
    public bool Empty => Remaining <= 0;
    public char Next => Source[Index];
    public string Substring(int len) => Source.Substring(Index, len);
    public char CharAt(int lookahead) => Source[Index + lookahead];
    public bool TryCharAt(int lookahead, out char chr) => Source.TryIndex(Index + lookahead, out chr);
    
    public bool IndexChanged(InputStream<S> previous) => Index != previous.Index;

    public InputStream(string description, string source, S state) {
        Source = source;
        State = state;
        Description = description;
        Position = new Position(0, 1, 0);
    }

    private InputStream(InputStream<S> prev, int? index, int? line, int? lineStartIndex) {
        Source = prev.Source;
        State = prev.State;
        Description = prev.Description;
        Position = new Position(
            index ?? prev.Index, 
            line ?? prev.Position.Line, 
            lineStartIndex ?? prev.Position.IndexOfLineStart);
    }

    public InputStream<S> Step(int step = 1) {
        if (Index + step > Source.Length)
            throw new Exception($"Step was called on {Description} without enough content in the source." +
                                "This means the caller provided an incorrect step value.");
        var nextLine = Position.Line;
        var nextLineStartIndex = Position.IndexOfLineStart;
        for (int ii = 0; ii < step; ++ii) {
            //If the current element is a newline, then the new element gets a new Line parameter.
            if (Source[Index + ii] == '\n') {
                ++nextLine;
                nextLineStartIndex = Index + ii + 1;
            }
        }
        return new InputStream<S>(this, Index + step, nextLine, nextLineStartIndex);
    }

    public LocatedParserError? MakeError(ParserError? p) => p == null ? null : new(Position, p);
}

/// <summary>
/// The status of a parse result.
/// </summary>
public enum ResultStatus {
    /// <summary>
    /// Parsing was successful.
    /// </summary>
    OK = 0,
    /// <summary>
    /// Parsing failed, but backtracking is enabled.
    /// </summary>
    ERROR = 1,
    /// <summary>
    /// Parsing failed and backtracking is not enabled.
    /// </summary>
    FATAL = 2
}
/// <summary>
/// The result of running a parser on a string.
/// </summary>
/// <typeparam name="R">Type of parser return value.</typeparam>
/// <typeparam name="S">Type of user state variable.</typeparam>
public readonly struct ParseResult<R, S> {
    public Maybe<R> Result { get; }
    /// <summary>
    /// Errors may be present even if the parsing was successful, specifically during no-consume successes.
    ///  Consider the example: \(A(,B)?\) which parses either (A) or (A,B). If we provide (AB) then the
    ///  error should print "expecting ',' or ')'", the first part of which is provided by the optional parser.
    /// Also, errors may *not* be present even if parsing fails. This is an uncommon case but it is possible.
    /// </summary>
    public LocatedParserError? Errors { get; }
    public InputStream<S> Previous { get; }
    public InputStream<S> Remaining { get; }
    
    public bool Consumed => Remaining.IndexChanged(Previous);
    
    public ResultStatus Status =>
        Result.Valid ?
            ResultStatus.OK :
            //FParsec logic. http://www.quanttec.com/fparsec/users-guide/looking-ahead-and-backtracking.html
            Consumed ?
                ResultStatus.FATAL :
                ResultStatus.ERROR;

    public ParseResult(Maybe<R> result, LocatedParserError? errors, InputStream<S> previous, InputStream<S> remaining) {
        Result = result;
        Errors = errors;
        Previous = previous;
        Remaining = remaining;
    }
    public ParseResult(ParserError errors, InputStream<S> previous, InputStream<S>? remaining = null) {
        Result = Maybe<R>.None;
        Errors = previous.MakeError(errors);
        Previous = previous;
        Remaining = remaining ?? previous;
    }

    public ParseResult<R, S> WithWrapError(string label) => 
        new(Result, Previous.MakeError(
            new ParserError.Labelled(label, new(Errors?.Error))), Previous, Remaining);
    
    public ParseResult<R, S> WithError(ParserError err) => 
        new(Result, new(Previous.Position, err), Previous, Remaining);

    public LocatedParserError? MergeErrors<R2>(ParseResult<R2, S> second) {
        if (!Errors.Try(out var f) || Consumed)
            return second.Errors;
        if (!second.Errors.Try(out var s))
            return Errors;
        return new LocatedParserError(Previous.Position, new ParserError.EitherOf(f.Error, s.Error));
    }

    public ParseResult<R, S> WithPrecedingError<R2>(ParseResult<R2, S> prev) => 
        new(Result, prev.MergeErrors(this), Previous, Remaining);

    public ParseResult<R2, S> FMap<R2>(Func<R, R2> f) => 
        new(Result.FMap(f), Errors, Previous, Remaining);

    public static ParseResult<R2, S> Apply<R2>(ParseResult<Func<R, R2>, S> fp, Parser<R, S> argp) {
        if (fp.Result.Try(out var v)) {
            var arg = argp(fp.Remaining);
            return new(arg.Result.FMap(v), fp.MergeErrors(arg), fp.Previous, arg.Remaining);
        } else {
            return fp.ForwardError<R2>();
        }
    }
    
    public ParseResult<R2, S> Bind<R2>(Func<R, Parser<R2, S>> fp) {
        if (Result.Try(out var r)) {
            var next = fp(r)(Remaining);
            return new(next.Result, MergeErrors(next), Previous, next.Remaining);
        } else
            return ForwardError<R2>();
    }
    
    public ParseResult<R3, S> Bind<R2, R3>(Func<R, Parser<R2, S>> fp, Func<R, R2, R3> project) {
        if (Result.Try(out var x)) {
            var next = fp(x)(Remaining);
            return new(next.Result.Try(out var y) ? new(project(x, y)) : Maybe<R3>.None, 
                MergeErrors(next), Previous, next.Remaining);
        } else
            return ForwardError<R3>();
    }
    public ParseResult<R3, S> Bind<R2, R3>(Parser<R2, S> fp, Func<R, R2, R3> project) {
        if (Result.Try(out var x)) {
            var next = fp(Remaining);
            return new(next.Result.Try(out var y) ? new(project(x, y)) : Maybe<R3>.None, 
                MergeErrors(next), Previous, next.Remaining);
        } else
            return ForwardError<R3>();
    }

    public ParseResult<R2, S> ForwardError<R2>() => 
        Result.Valid ? 
            throw new Exception("ToErrorType should not be called on non-error parse results") :
            new(Maybe<R2>.None, Errors, Previous, Remaining);
    
    
    //Efficient implementations for common use cases
    public ParseResult<R, S> BindThenReturnThis<R2>(Parser<R2, S> fp) {
        if (Result.Try(out var x)) {
            var next = fp(Remaining);
            return new(next.Result.Try(out var y) ? Result : Maybe<R>.None, 
                MergeErrors(next), Previous, next.Remaining);
        } else
            return this;
    }
    public ParseResult<R2, S> BindThenReturnNext<R2>(Parser<R2, S> fp) {
        if (Result.Try(out var x)) {
            var next = fp(Remaining);
            return new(next.Result.Try(out var y) ? new(y) : Maybe<R2>.None, 
                MergeErrors(next), Previous, next.Remaining);
        } else
            return ForwardError<R2>();
    }
    public ParseResult<(R, R2), S> BindThenReturnTuple<R2>(Parser<R2, S> fp) {
        if (Result.Try(out var x)) {
            var next = fp(Remaining);
            return new(next.Result.Try(out var y) ? new((x, y)) : Maybe<(R, R2)>.None, 
                MergeErrors(next), Previous, next.Remaining);
        } else
            return ForwardError<(R, R2)>();
    }
    
    public ParseResult<R2, S> BindThenReturnMiddle<R2, R3>(Parser<R2, S> middle, Parser<R3, S> right) {
        if (!Result.Valid) return ForwardError<R2>();
        var next = middle(Remaining);
        if (!next.Result.Try(out var x)) return next.ForwardError<R2>();
        var outer = right(next.Remaining);
        if (!outer.Result.Valid) return outer.ForwardError<R2>();
        return new(x, next.Errors, Previous, outer.Remaining);
    }
    
}

/// <summary>
/// A parser is a function that accepts a string to parse and an initial state,
/// passes it through parsing code,
/// and returns a parse result (which, loosely speaking, contains either a result value or an error).
/// </summary>
/// <param name="input"></param>
/// <typeparam name="R"></typeparam>
/// <typeparam name="S"></typeparam>
public delegate ParseResult<R, S> Parser<R, S>(InputStream<S> input);



}