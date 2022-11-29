using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Functional;

namespace Mizuhashi {
/// <summary>
/// A description of a position in a stream, including line and column information.
/// </summary>
public readonly struct Position {
    /// <summary>
    /// Index in the source string (0-indexed) of the next character.
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

    public Position(string source, int index) {
        Index = index;
        var nextLine = 1;
        var nextLineStartIndex = 0;
        for (int ii = 0; ii < index; ++ii) {
            if (source[ii] == '\n') {
                ++nextLine;
                nextLineStartIndex = ii + 1;
            }
        }
        Line = nextLine;
        IndexOfLineStart = nextLineStartIndex;
    }

    /// <summary>
    /// </summary>
    /// <param name="source">Source string</param>
    /// <param name="line">Line number (1-indexed)</param>
    /// <param name="column">Column number (1-indexed)</param>
    public Position(string source, int line, int column) {
        var currLine = 1;
        var currLineStartIndex = 0;
        int ii = 0;
        for (; (currLine < line || currLine == line && (ii - currLineStartIndex + 1) < column) && ii < source.Length; ++ii) {
            if (source[ii] == '\n') {
                ++currLine;
                currLineStartIndex = ii + 1;
            }
        }
        Index = ii;
        Line = currLine;
        IndexOfLineStart = currLineStartIndex;
    }

    /// <summary>
    /// Return this position with the index increased by one.
    /// </summary>
    /// <returns></returns>
    public Position Increment() => new(Index + 1, Line, IndexOfLineStart);

    public override string ToString() => Print(false);
    public string Print(bool compact) => compact ?
        $"{Line}:{Column}" :
        $"Line {Line}, Col {Column}";

    public bool Equals(Position other) => this == other;
    public override bool Equals(object? obj) => obj is Position other && Equals(other);
    public override int GetHashCode() => Tuple.GetHashCode();
    public static bool operator ==(Position a, Position b) => a.Tuple == b.Tuple;
    public static bool operator !=(Position a, Position b) => !(a == b);
}

/// <summary>
/// A description of a position range in a stream, with an inclusive start and exclusive end.
/// <br/>When printed, the columns are printed inclusively.
/// </summary>
public readonly struct PositionRange {
    public Position Start { get; }
    public Position End { get; }
    public bool Empty => End.Index <= Start.Index;
    private (Position, Position) Tuple => (Start, End);
    public PositionRange(in Position start, in Position end) {
        Start = start;
        End = end;
    }

    public override string ToString() => Print(false);
    public string Print(bool compact) {
        if (Start.Line == End.Line) {
            if (End.Column - 1 > Start.Column)
                return compact ?
                    $"{Start.Line}:{Start.Column}-{End.Column}" :
                    $"Line {Start.Line}, Cols {Start.Column}-{End.Column}";
            else
                return $"{Start.Print(compact)}";
        } else {
            var sep = compact ? "-" : " to ";
            return $"{Start.Print(compact)}{sep}{End.Print(compact)}";
        }
    }

    public PositionRange Merge(in PositionRange second) => new(Start, second.End);

    public bool Contains(in Position p) => Start.Index <= p.Index && p.Index < End.Index;
    public bool ContainsInclusiveEnd(in Position p) => Start.Index <= p.Index && p.Index <= End.Index;
    
    public bool Equals(PositionRange other) => this == other;
    public override bool Equals(object? obj) => obj is PositionRange other && Equals(other);
    public override int GetHashCode() => Tuple.GetHashCode();
    public static bool operator ==(PositionRange a, PositionRange b) => a.Tuple == b.Tuple;
    public static bool operator !=(PositionRange a, PositionRange b) => !(a == b);
}

public readonly struct InputStreamState {
    /// <summary>
    /// Current position of the stream.
    /// </summary>
    public Position Position { get; }
    /// <summary>
    /// User-defined state variable.
    /// </summary>
    public object State { get; }
    
    public InputStreamState(Position pos, object state) {
        Position = pos;
        State = state;
    }
}
/// <summary>
/// A lightweight description of a parseable string.
/// <br/>A single mutable instance of this is threaded through the parsing process.
/// </summary>
public class InputStream {
    /// <summary>
    /// Source string.
    /// </summary>
    public string Source { get; }
    /// <summary>
    /// Human-readable description of this parseable stream.
    /// </summary>
    public string Description { get; }
    
    /// <summary>
    /// Mutable state of the input string.
    /// <br/>Design-wise, it is possible to extract this into ParserResult and make the stream immutable,
    /// but that's more expensive.
    /// </summary>
    public InputStreamState Stative { get; private set; }
    public List<LocatedParserError?> Rollbacks { get; } = new();

    public int Index => Stative.Position.Index;
    public int Remaining => Source.Length - Index;
    public bool Empty => Index >= Source.Length;
    public char Next => Source[Index];
    public char? MaybeNext => Index < Source.Length ? Source[Index] : null;
    public string Substring(int len) => Source.Substring(Index, len);
    public string Substring(int offset, int len) => Source.Substring(Index + offset, len);
    public char CharAt(int lookahead) => Source[Index + lookahead];
    public bool TryCharAt(int lookahead, out char chr) => Source.TryIndex(Index + lookahead, out chr);
    

    public InputStream(string description, string source, object state) {
        Source = source;
        Stative = new(new Position(0, 1, 0), state);
        Description = description;
    }

    public void UpdateState(object newState) {
        Stative = new (Stative.Position, newState);
    }

    public void Rollback(InputStreamState ss, LocatedParserError? error) {
        Rollbacks.Add(error);
        Stative = ss;
    }

    public int Step(int step = 1) {
        if (step == 0) return Index;
        if (Index + step > Source.Length)
            throw new Exception($"Step was called on {Description} without enough content in the source." +
                                "This means the caller provided an incorrect step value.");
        var nextLine = Stative.Position.Line;
        var nextLineStartIndex = Stative.Position.IndexOfLineStart;
        for (int ii = 0; ii < step; ++ii) {
            //If the current element is a newline, then the new element gets a new Line parameter.
            if (Source[Index + ii] == '\n') {
                ++nextLine;
                nextLineStartIndex = Index + ii + 1;
            }
        }
        Stative = new InputStreamState(new Position(Index + step, nextLine, nextLineStartIndex), Stative.State);
        return Index;
    }

    public LocatedParserError? MakeError(ParserError? p) => p == null ? null : new(Index, p);

    /// <summary>
    /// Returns an error string containing all rollback information and the final error.
    /// </summary>
    public string ShowAllFailures(LocatedParserError final) {
        var errs = new List<string> {final.Show(Source)};
        errs.AddRange(Rollbacks
            .FilterNone()
            .Select(err => 
                $"The parser backtracked after the following error:\n\t{err.Show(Source).Replace("\n", "\n\t")}"));
        return string.Join("\n\n", errs);
    }
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
public readonly struct ParseResult<R> {
    public Maybe<R> Result { get; }
    /// <summary>
    /// Errors may be present even if the parsing was successful, specifically during no-consume successes.
    ///  Consider the example: \(A(,B)?\) which parses either (A) or (A,B). If we provide (AB) then the
    ///  error should print "expected ',' or ')'", the first part of which is provided by the optional parser.
    /// <br/>Errors are always present if parsing fails.
    /// </summary>
    public LocatedParserError? Error { get; }
    public LocatedParserError ErrorOrThrow => Error ?? throw new Exception("Missing error");
    public int Start { get; }
    public int End { get; }

    public bool Consumed => End > Start;
    public ResultStatus Status =>
            (Result.Valid ?
                ResultStatus.OK :
                //FParsec logic. http://www.quanttec.com/fparsec/users-guide/looking-ahead-and-backtracking.html
                Consumed ?
                    ResultStatus.FATAL :
                    ResultStatus.ERROR);

    public ParseResult(Maybe<R> result, LocatedParserError? error, int start, int end) {
        Result = result;
        Error = error;
        Start = start;
        End = end;
    }
    public ParseResult(ParserError errors, int start, int? end = null) :
        this(new LocatedParserError(start, errors), start, end) { }
    public ParseResult(LocatedParserError? err, int start, int? end = null) {
        Result = Maybe<R>.None;
        Error = err ?? throw new Exception("Missing error");
        Start = start;
        End = end ?? start;
    }

    public ParseResult<R> WithWrapError(string label) => 
        new(Result, Error.Try(out var e) ? new LocatedParserError(e.Index, 
            new ParserError.Labelled(label, new(Error))) : null, Start, End);

    public LocatedParserError? MergeErrors<B>(in ParseResult<B> second) {
        if (second.Consumed)
            return second.Error;
        return LocatedParserError.Merge(Error, second.Error);
    }

    public ParseResult<R> WithPreceding<R2>(in ParseResult<R2> prev) => 
            new(Result, prev.MergeErrors(this), prev.Start, End);

    public ParseResult<R2> FMap<R2>(Func<R, R2> f) => 
        new(Result.FMap(f), Error, Start, End);

    public ParseResult<R2> CastFailure<R2>() => 
        Result.Valid ? 
            throw new Exception($"{nameof(CastFailure)} should not be called on non-error parse results") :
            new(Maybe<R2>.None, Error, Start, End);

}

/// <summary>
/// A parser is a function that accepts a string to parse and an initial state,
/// passes it through parsing code,
/// and returns a parse result (which, loosely speaking, contains either a result value or an error).
/// </summary>
public delegate ParseResult<R> Parser<R>(InputStream input);



}