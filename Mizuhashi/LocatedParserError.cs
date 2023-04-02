using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace Mizuhashi {
/// <summary>
/// Abstract type for errors that occur while parsing.
/// </summary>
public abstract record ParserError {
    public Position? StartingFrom { get; init; } = null;
    
    /// <summary>
    /// Convert this error and all nested errors into a flattened list.
    /// </summary>
    /// <returns></returns>
    protected virtual IEnumerable<ParserError> Enumerated() => new[]{this};
    
    /// <summary>
    /// Create an <see cref="EitherOf"/> error.
    /// </summary>
    public virtual ParserError JoinWith(ParserError other) => new EitherOf(this, other);
    
    /// <summary>
    /// Pretty-print the error contents.
    /// <br/>This does not print the position prelude, but nested errors may print their own prelude.
    /// </summary>
    public abstract string Show(IInputStream s, int streamIndex);

    /// <summary>
    /// Show a top-level error. This always prints the position prelude.
    /// </summary>
    public string ShowTopLevel(IInputStream s, int streamIndex) {
        var e = this.Flatten();
        return $"{s.TokenWitness.ShowErrorPosition(new(streamIndex, e))}\n{e.Show(s, streamIndex)}";
    }

    /// <summary>
    /// Show a nested error. This prints the position prelude iff <see cref="StartingFrom"/> is nonempty.
    /// </summary>
    public string ShowNested(IInputStream s, int streamIndex) {
        if (StartingFrom != null)
            //add newline for disambiguation
            return $"\n{s.TokenWitness.ShowErrorPosition(new(streamIndex, this))}\n{Show(s, streamIndex)}";
        else
            return Show(s, streamIndex);
    }

    /// <summary>
    /// Error representing generic failure.
    /// </summary>
    public record Failure(string Message) : ParserError {
        /// <inheritdoc/>
        public override string Show(IInputStream s, int streamIndex) => Message;
    }

    /// <summary>
    /// Error representing a character that was expected but not found.
    /// </summary>
    public record ExpectedChar(char Char) : ParserError {
        /// <inheritdoc/>
        public override string Show(IInputStream s, int streamIndex) => $"Expected '{Char}'.";
    }
    
    /// <summary>
    /// Error representing a string that was expected but not found.
    /// </summary>
    public record Expected(string String) : ParserError { 
        /// <inheritdoc/>
        public override string Show(IInputStream s, int streamIndex) => $"Expected {String}.";
    }

    /// <summary>
    /// Error representing that the number of parsed objects was incorrect.
    /// </summary>
    public record IncorrectNumber(int Required, int Received, string? ObjectDescription, LocatedParserError? Inner) : ParserError {
        /// <inheritdoc/>
        public override string Show(IInputStream s, int streamIndex) {
            var objSuffix = ObjectDescription == null ? "" : $" of {ObjectDescription}";
            var excSuffix = Inner == null ? "" : "\n\t" + Inner.Value.Show(s).Replace("\n", "\n\t");
            return $"Required {Required} values{objSuffix}, but only received {Received}." + excSuffix;
        }
    }
    
    /// <summary>
    /// Error due to a character that was not expected.
    /// </summary>
    public record UnexpectedChar(char Char) : ParserError { 
        /// <inheritdoc/>
        public override string Show(IInputStream s, int streamIndex) => $"Did not expect '{Char}'.";
        
    }
   
    /// <summary>
    /// Error due a string that was not expected.
    /// </summary>
    public record Unexpected(string String) : ParserError { 
            /// <inheritdoc/>
        public override string Show(IInputStream s, int streamIndex) => $"Did not expect {String}.";
    }
    
    /// <summary>
    /// A wrapper around an error with a label that describes what it was supposed to parse.
    /// </summary>
    public record Labelled(string Label, Either<string, ParserError?> ExpectedContent) : ParserError {
        /// <inheritdoc/>
        public override string Show(IInputStream s, int streamIndex) => ExpectedContent.Map(
            err => $"Failed while parsing {Label}.\n{err}",
            err => err is null ?
                $"Expected {Label}." :
                $"Failed while parsing {Label}.\n{err.ShowNested(s, streamIndex)}"
        );
    }

    /// <summary>
    /// Two errors, either of which would resolve parsing if fixed.
    /// </summary>
    public record EitherOf(ParserError OptionA, ParserError OptionB) : ParserError {
        /// <inheritdoc/>
        protected override IEnumerable<ParserError> Enumerated() => OptionA.Enumerated().Concat(OptionB.Enumerated());
        
        /// <inheritdoc/>
        public override string Show(IInputStream s, int streamIndex) {
            return "Static failure: EitherOf direct show: This should not appear.";
        }
    }

    /// <summary>
    /// Multiple errors, any of which would fix parsing if resolved.
    /// </summary>
    public record OneOf(List<ParserError> Errors) : ParserError {
        /// <inheritdoc/>
        protected override IEnumerable<ParserError> Enumerated() => Errors.SelectMany(e => e.Enumerated());
        /// <inheritdoc/>
        public override ParserError JoinWith(ParserError other) {
            Errors.Add(other);
            return this;
        }

        /// <inheritdoc/>
        public override string Show(IInputStream s, int streamIndex) {
            if (Errors.Count == 1)
                return Errors[0].Show(s, streamIndex);
            return "Resolve any of the following errors to continue.\n\t" + string.Join("\n\t",
                Errors.Select(e => "- " + e.ShowNested(s, streamIndex).Replace("\n", "\n\t  ")));
        }

        /// <summary>
        /// Overrides the equality operator to do element-wise checks on <see cref="Errors"/>.
        /// </summary>
        public virtual bool Equals(OneOf? other) =>
            other != null && Errors.Count == other.Errors.Count &&
            Enumerable.Range(0, Errors.Count).All(i => Errors[i] == other.Errors[i]);
    }
    
    /// <summary>
    /// Flatten an error by calling <see cref="Enumerated"/> and wrapping the result in <see cref="OneOf"/> if necessary.
    /// </summary>
    public ParserError Flatten() =>
        this switch {
            EitherOf e => new OneOf(e.Enumerated().ToList()),
            OneOf e => new OneOf(e.Enumerated().ToList()),
            Labelled { ExpectedContent: {IsRight: true, Right: { } r } } e => 
                e with { ExpectedContent = new(r.Flatten()) },
            _ => this
        };

    /// <summary>
    /// Convert a string into an <see cref="Expected"/> error.
    /// </summary>
    public static implicit operator ParserError?(string? s) => s == null ? null : new Expected(s);
}

/// <summary>
/// A <see cref="ParserError"/> paired with a location in the source string.
/// </summary>
public readonly struct LocatedParserError {
    //Store Index instead of position because it's more space-efficient. We can expand back to position if there are parsing errors
    /// <summary>
    /// The index in the source stream where the error occured.
    /// </summary>
    public readonly int Index;
    /// <summary>
    /// Error.
    /// </summary>
    public readonly ParserError Error;

    private (int, ParserError) Tuple => (Index, Error);

    /// <summary>
    /// Create a <see cref="LocatedParserError"/>.
    /// </summary>
    public LocatedParserError(int index, ParserError error) {
        Index = index;
        Error = error;
    }

    /// <summary>
    /// Display the error in the source stream.
    /// </summary>
    public string Show(IInputStream source) => Error.ShowTopLevel(source, Index);
    
    /// <summary>
    /// Join two errors as efficiently as possible.
    /// </summary>
    public static LocatedParserError? Merge(LocatedParserError? first, LocatedParserError? second) {
        if (!first.HasValue)
            return second;
        if (!second.HasValue)
            return first;
        var f = first.Value;
        var s = second.Value;
        if (f.Index != s.Index)
            return s;
        return new LocatedParserError(f.Index, f.Error.JoinWith(s.Error));
    }
    /// <summary>
    /// Equality operator. Test that the index and error are the same.
    /// </summary>
    public bool Equals(LocatedParserError other) => this == other;
    
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is LocatedParserError other && Equals(other);
    
    /// <inheritdoc/>
    public override int GetHashCode() => (Location: Index, Error).GetHashCode();
    
    /// <inheritdoc cref="Equals(Mizuhashi.LocatedParserError)"/>
    public static bool operator ==(LocatedParserError a, LocatedParserError b) => a.Tuple == b.Tuple;
    
    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(LocatedParserError a, LocatedParserError b) => !(a == b);
}
}