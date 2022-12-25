using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib;
using BagoumLib.Functional;

namespace Mizuhashi {
/// <summary>
/// A description of a position in a string, including line and column information.
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

    /// <summary>
    /// Find the position of the given index in the source string.
    /// <br/>Note: this is an O(n) operation in the size of `index`.
    /// </summary>
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

    /// <summary>
    /// Advance `steps` characters, using `over` to determine the locations of newlines.
    /// <br/>Assumes that `over` is some substring Source[Index..].
    /// </summary>
    public Position Step(ReadOnlySpan<char> over, int steps = 1) {
        var nextLine = Line;
        var nextLineStartIndex = IndexOfLineStart;
        for (int ii = 0; ii < steps; ++ii) {
            if (over[ii] == '\n') {
                ++nextLine;
                nextLineStartIndex = Index + ii + 1;
            }
        }
        return new(Index + steps, nextLine, nextLineStartIndex);
    }

    /// <summary>
    /// Creates a position range between this position and `steps` characters ahead, using
    /// `over` to determine the locations of newlines.
    /// <br/>Assumes that `over` is some substring Source[Index..].
    /// </summary>
    public PositionRange CreateRange(ReadOnlySpan<char> over, int steps) => new(this, Step(over, steps));

    /// <inheritdoc/>
    public override string ToString() => Print(false);
    
    /// <summary>
    /// Print the position.
    /// </summary>
    /// <param name="compact">Whether or not the position should be printed in a compact form.</param>
    public string Print(bool compact) => compact ?
        $"{Line}:{Column}" :
        $"Line {Line}, Col {Column}";

    /// <summary>
    /// Print the entire line of this position in the source string.
    /// </summary>
    public string ShowLine(string source) {
        var lineLength = 0;
        //for (int ii = pos.IndexOfLineStart; ii < pos.Index; ++ii)
        //    spacesSB.Append(source[ii] == '\t' ? '\t' : source[ii]);// '.');
        for (; IndexOfLineStart + lineLength < source.Length && 
               source[IndexOfLineStart + lineLength] != '\n'; ++lineLength) { }
        return source.Substring(IndexOfLineStart, lineLength);
    }

    /// <summary>
    /// Print the contents of this position's line in the source string, up to but not including this position.
    /// </summary>
    public string ShowLineUpToPosition(string source) {
        var spacesSB = new StringBuilder();
        for (int ii = IndexOfLineStart; ii < Index; ++ii)
            spacesSB.Append(source[ii] == '\t' ? '\t' : source[ii]);// '.');
        return spacesSB.ToString();
    }

    /// <summary>
    /// Show the contents of this position in a user-friendly way by combining ShowLine and ShowLineUpToPosition. 
    /// </summary>
    public string PrettyPrintLocation(string source) => $"{ShowLine(source)}\n" +
                                                 $"{ShowLineUpToPosition(source)}| <- at this location";

    /// <summary>
    /// Equality operator.
    /// </summary>
    public bool Equals(Position other) => this == other;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Position other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => Tuple.GetHashCode();
    /// <inheritdoc cref="Equals(Mizuhashi.Position)"/>
    public static bool operator ==(Position a, Position b) => a.Tuple == b.Tuple;
    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Position a, Position b) => !(a == b);
}

/// <summary>
/// A description of a position range in a stream, with an inclusive start and exclusive end.
/// <br/>When printed, the columns are printed inclusively.
/// </summary>
public readonly struct PositionRange {
    /// <summary>
    /// Start point of the range (inclusive).
    /// </summary>
    public Position Start { get; }
    
    /// <summary>
    /// End point of the range (exclusive).
    /// </summary>
    public Position End { get; }
    
    /// <summary>
    /// True iff the range contains no elements.
    /// </summary>
    public bool Empty => End.Index <= Start.Index;
    private (Position, Position) Tuple => (Start, End);
    public PositionRange(in Position start, in Position end) {
        Start = start;
        End = end;
    }

    /// <inheritdoc/>
    public override string ToString() => Print(false);
    
    /// <summary>
    /// Print the range.
    /// </summary>
    /// <param name="compact">True iff the range should be printed in a compact form.</param>
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

    /// <summary>
    /// Create a new range that starts where this range starts and ends where the second range ends.
    /// </summary>
    public PositionRange Merge(in PositionRange second) => new(Start, second.End);

    /// <summary>
    /// True iff a position is contained within this range.
    /// </summary>
    public bool Contains(in Position p) => Start.Index <= p.Index && p.Index < End.Index;
    /// <summary>
    /// True iff a position is contained within this range, or is equal to the end position of the range.
    /// </summary>
    public bool ContainsInclusiveEnd(in Position p) => Start.Index <= p.Index && p.Index <= End.Index;
    
    /// <summary>
    /// Equality operator.
    /// </summary>
    public bool Equals(PositionRange other) => this == other;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PositionRange other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => Tuple.GetHashCode();
    /// <inheritdoc cref="Equals(Mizuhashi.PositionRange)"/>
    public static bool operator ==(PositionRange a, PositionRange b) => a.Tuple == b.Tuple;
    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(PositionRange a, PositionRange b) => !(a == b);
}

/// <summary>
/// A small struct representing the current state of an input stream.
/// </summary>
public readonly struct InputStreamState {
    /// <summary>
    /// Index of the next token in the stream. (This is the index in the token array.)
    /// </summary>
    public int Index { get; }
    
    /// <summary>
    /// Position of the next token in the source string, which may not be directly visible to the input stream.
    /// <br/>Note that SourcePosition.Index is an index in the source string, NOT in the token array.
    /// </summary>
    public Position SourcePosition { get; }
    
    /// <summary>
    /// User-defined state variable.
    /// </summary>
    public object State { get; }

    public InputStreamState(int streamPosition, Position stringPosition, object state) {
        Index = streamPosition;
        SourcePosition = stringPosition;
        State = state;
    }
}

/// <summary>
/// Base interface for parseable input streams, without the token type specified.
/// </summary>
public interface IInputStream {
    /// <summary>
    /// Witness describing how to display the tokens over some user-facing source string.
    /// </summary>
    public ITokenWitness TokenWitness { get; }
    
    /// <summary>
    /// Returns an error string for an error.
    /// </summary>
    public string ShowError(LocatedParserError err);
}

/// <summary>
/// A lightweight description of a parseable stream.
/// <br/>A single mutable instance of this is threaded through the parsing process.
/// </summary>
public class InputStream<Token> : IInputStream {
    /// <inheritdoc/>
    public ITokenWitness TokenWitness { get; }
    /// <summary>
    /// Source stream.
    /// </summary>
    public Token[] Source { get; }
    
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
    public List<LocatedParserError> Rollbacks { get; } = new();

    public int Index => Stative.Index;
    public int Remaining => Source.Length - Index;
    public bool Empty => Index >= Source.Length;
    public Token Next => Source[Index];
    public Token? MaybeNext => Index < Source.Length ? Source[Index] : default(Token?);
    //public string Substring(int len) => Source.Substring(Index, len);
    //public string Substring(int offset, int len) => Source.Substring(Index + offset, len);
    public Token CharAt(int lookahead) => Source[Index + lookahead];
    public bool TryCharAt(int lookahead, out Token chr) => Source.Try(Index + lookahead, out chr);
    

    public InputStream(string description, Token[] source, object state, ITokenWitnessCreator<Token>? witnessCreator = null) {
        Source = source;
        Stative = new(0, new Position(0, 1, 0), state);
        Description = description;
        if (witnessCreator == null && typeof(Token) == typeof(char))
            witnessCreator = (new CharTokenWitnessCreator()) as ITokenWitnessCreator<Token>;
        TokenWitness = (witnessCreator ?? throw new Exception("Witness creator not provided for input stream")).Create(this);
    }

    public void UpdateState(object newState) {
        Stative = new (Stative.Index, Stative.SourcePosition, newState);
    }

    /// <summary>
    /// Rollback the position of the stream, storing the error that caused the rollback.
    /// </summary>
    public void Rollback(in InputStreamState ss, in LocatedParserError? error) {
        if (error.HasValue)
            Rollbacks.Add(error.Value);
        Stative = ss;
    }

    /// <summary>
    /// Rollback the position of the stream.
    /// </summary>
    public void RollbackFast(in InputStreamState ss) {
        Stative = ss;
    }

    public int Step(int step = 1) {
        if (step == 0) return Index;
        if (Index + step > Source.Length)
            throw new Exception($"Step was called on {Description} without enough content in the source." +
                                "This means the caller provided an incorrect step value.");
        Stative = new(Stative.Index + step, TokenWitness.Step(step), Stative.State);
        return Index;
    }

    public LocatedParserError? MakeError(ParserError? p) => p == null ? null : new(Index, p);

    /// <summary>
    /// Returns an error string containing all rollback information and the final error.
    /// </summary>
    public string ShowAllFailures(LocatedParserError final) {
        var errs = new List<string> {ShowError(final)};
        errs.AddRange(Rollbacks
            .Select(err => 
                $"The parser backtracked after the following error:\n\t{ShowError(err).Replace("\n", "\n\t")}"));
        return string.Join("\n\n", errs);
    }

    /// <inheritdoc/>
    public string ShowError(LocatedParserError err) => TokenWitness.ShowError(err);
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
    /// <summary>
    /// Result value. If this is present, then parsing succeeded.
    /// </summary>
    public Maybe<R> Result { get; }
    
    /// <summary>
    /// Errors produced by parsing.
    /// <br/>Note that errors may be present even if the parsing was successful, specifically during no-consume successes.
    ///  Consider the example: \(A(,B)?\) which parses either (A) or (A,B). If we provide (AB) then the
    ///  error should print "expected ',' or ')'", the first part of which is provided by the optional parser.
    /// <br/>Errors are always present if parsing fails.
    /// </summary>
    public LocatedParserError? Error { get; }
    
    /// <summary>
    /// Get <see cref="Error"/> or throw an exception.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public LocatedParserError ErrorOrThrow => Error ?? throw new Exception("Missing error");
    
    /// <summary>
    /// Starting index (inclusive) of the parsed result.
    /// </summary>
    public int Start { get; }
    
    /// <summary>
    /// Ending index (exclusive) of the parsed result.
    /// </summary>
    public int End { get; }

    /// <summary>
    /// True iff a nonzero number of characters were consumed to produce the result.
    /// </summary>
    public bool Consumed => End > Start;

    public string ShowConsumed(string s) => s[Start..End];
    public PositionRange ShowPosition(string s) => new(new(s, Start), new(s, End));
    
    /// <summary>
    /// Result status of parsing.
    /// </summary>
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

    /// <summary>
    /// Join the errors and consumption range of this parse result with a preceding parse result.
    /// <br/>Use this whenever parsers are run in sequence.
    /// </summary>
    public ParseResult<R> WithPreceding<R2>(in ParseResult<R2> prev) => 
            new(Result, prev.MergeErrors(this), prev.Start, End);

    /// <summary>
    /// Map over the value of a parse result.
    /// </summary>
    public ParseResult<R2> FMap<R2>(Func<R, R2> f) => 
        new(Result.FMap(f), Error, Start, End);

    /// <summary>
    /// Change the value of a successful parse result.
    /// </summary>
    public ParseResult<R2> WithResult<R2>(R2 result) =>
        Result.Valid ?
            new(result, Error, Start, End) :
            throw new Exception($"{nameof(WithResult)} should not be called on error parse results");

    /// <summary>
    /// Convert a successful parse result into an error.
    /// </summary>
    public ParseResult<R2> AsError<R2>(LocatedParserError error, bool dontConsume = true) =>
        Result.Valid ?
            new(Maybe<R2>.None, error, Start, dontConsume ? Start : End) :
            throw new Exception($"{nameof(AsError)} should not be called on error parse results");
    
    /// <inheritdoc cref="AsError{R2}(Mizuhashi.LocatedParserError,bool)"/>
    public ParseResult<R2> AsError<R2>(ParserError error, bool dontConsume = true) => AsError<R2>(new LocatedParserError(Start, error));

    /// <summary>
    /// Change the type of a failed parse result.
    /// </summary>
    public ParseResult<R2> CastFailure<R2>() => 
        Result.Valid ? 
            throw new Exception($"{nameof(CastFailure)} should not be called on non-error parse results") :
            new(Maybe<R2>.None, Error, Start, End);

}

/// <summary>
/// A parser is a function that accepts a stream to parse and an initial state,
/// passes it through parsing code,
/// and returns a parse result (which, loosely speaking, contains either a result value or an error).
/// </summary>
public delegate ParseResult<R> Parser<T, R>(InputStream<T> input);


/// <summary>
/// A degenerate class that defines how to display a stream over tokens.
/// </summary>
public interface ITokenWitness {
    /// <summary>
    /// Display an error in the source stream.
    /// </summary>
    public string ShowError(LocatedParserError error);

    /// <summary>
    /// Create an error showing that the token at the given index was unexpected.
    /// </summary>
    public ParserError Unexpected(int index);

    /// <summary>
    /// Called by the stream when it advances. Returns the new source code position.
    /// </summary>
    public Position Step(int step = 1);

    /// <summary>
    /// Convert an range in a stream to a range in some user-facing source string.
    /// <br/>eg. if this is operating over a tokenized stream of ["foo", "bar"], which has been lexed from a soure string "foo bar", then ToPosition(1, 2), indicating the position of "bar", should be Range(4, 7).
    /// <br/>If the start and end index are the same, then this should return an empty position range.
    /// </summary>
    public PositionRange ToPosition(int start, int end);

    /// <summary>
    /// Convert the range parsed by the parse-result to a range in some user-facing source string.
    /// </summary>
    public PositionRange ToPosition<R>(ParseResult<R> parsed) =>
        ToPosition(parsed.Start, parsed.End);

    /// <summary>
    /// Show the content of some user-facing source string that corresponds to the range [startIndex, endIndex) in the typed stream.
    /// </summary>
    public string ShowConsumed(int start, int end);

    /// <summary>
    /// Show the content of some user-facing source string that corresponds to the range parsed by the parse-result in the typed stream.
    /// </summary>
    public string ShowConsumed<R>(ParseResult<R> parsed) => 
        ShowConsumed(parsed.Start, parsed.End);
}

/// <summary>
/// A degenerate class that defines how to create an <see cref="ITokenWitness"/> from a stream.
/// </summary>
public interface ITokenWitnessCreator<T> {
    /// <summary>
    /// Create a token witness linked to a stream.
    /// </summary>
    public ITokenWitness Create(InputStream<T> stream);
}

/// <summary>
/// A degenerate class that defines how to display string (char[]) streams.
/// </summary>
public record CharTokenWitness(InputStream<char> Stream) : ITokenWitness {
    private readonly string source = new(Stream.Source);

    /// <inheritdoc/>
    public string ShowError(LocatedParserError error) {
        var pos = new Position(source, error.Index);
        return $"Error at {pos.ToString()}:\n" +
               pos.PrettyPrintLocation(source) +
               $"\n{error.Error.Flatten().Show(Stream)}";
    }

    /// <inheritdoc/>
    public ParserError Unexpected(int index) => new ParserError.UnexpectedChar(Stream.Source[index]);

    /// <inheritdoc/>
    public Position Step(int step = 1) {
        var st = Stream.Stative;
        var lineNum = st.SourcePosition.Line;
        var lineStartIndex = st.SourcePosition.IndexOfLineStart;
        for (int ii = 0; ii < step; ++ii) {
            //If the current element is a newline, then the new element gets a new Line parameter.
            if (Stream.Source[st.Index + ii] == '\n') {
                ++lineNum;
                lineStartIndex = st.Index + ii + 1;
            }
        }
        //note that we're using st.Index to derive SourcePosition.Index, which is only sound because this is a char array
        return new Position(st.Index + step, lineNum, lineStartIndex);
    }

    /// <inheritdoc/>
    public PositionRange ToPosition(int start, int end) {
        return new(new(source, start), new(source, end));
    }

    /// <inheritdoc/>
    public string ShowConsumed(int start, int end) {
        return new string(source[start..end]);
    }
}

/// <summary>
/// A degenerate class that defines how to create <see cref="CharTokenWitness"/>.
/// </summary>
public class CharTokenWitnessCreator : ITokenWitnessCreator<char> {
    /// <inheritdoc/>
    public ITokenWitness Create(InputStream<char> stream)
        => new CharTokenWitness(stream);
}

}