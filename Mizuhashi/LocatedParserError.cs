using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace Mizuhashi {
public abstract record ParserError {
    protected virtual IEnumerable<ParserError> Enumerated() => new[]{this};

    public record Failure(string Message) : ParserError {
        public override string ToString() => $"Failure: {Message}";
    }

    public record ExpectedChar(char Char) : ParserError {
        public override string ToString() => $"Expected '{Char}'";
    }
    public record Expected(string String) : ParserError { 
        public override string ToString() => $"Expected {String}";
    }
    public record UnexpectedChar(char Char) : ParserError { 
        public override string ToString() => $"Did not expect '{Char}'";
        
    }
    public record Unexpected(string String) : ParserError { 
        public override string ToString() => $"Did not expect {String}";
    }
    
    public record Labelled(string Label, Either<string, ParserError?> ExpectedContent) : ParserError {
        public override string ToString() => ExpectedContent.Map(
            err => $"Failed in parsing {Label}: {err}",
            err => $"Failed in parsing {Label}" + (err == null ?
                "" :
                ":\n\t" + err.ToString().Replace("\n", "\n\t"))
        );
    }

    public record EitherOf(ParserError OptionA, ParserError OptionB) : ParserError {
        protected override IEnumerable<ParserError> Enumerated() => OptionA.Enumerated().Concat(OptionB.Enumerated());
    }

    public record OneOf(List<ParserError> Errors) : ParserError {
        protected override IEnumerable<ParserError> Enumerated() => Errors.SelectMany(e => e.Enumerated());

        public override string ToString() {
            if (Errors.Count == 1)
                return Errors[0].ToString();
            return "Parsing failed. Resolve any of the following errors to continue.\n\t" + string.Join("\n",
                Errors.Select(e => e.ToString())).Replace("\n", "\n\t");
        }

        public virtual bool Equals(OneOf? other) =>
            other != null && Errors.Count == other.Errors.Count &&
            Enumerable.Range(0, Errors.Count).All(i => Errors[i] == other.Errors[i]);
    }

    public ParserError Flatten() =>
        this switch {
            EitherOf e => new OneOf(e.Enumerated().ToList()),
            OneOf e => new OneOf(e.Enumerated().ToList()),
            _ => this
        };

    public static implicit operator ParserError?(string? s) => s == null ? null : new Expected(s);
}
public readonly struct LocatedParserError {
    public Position Location { get; }
    public ParserError Error { get; }

    private (Position, ParserError) Tuple => (Location, Error);

    public LocatedParserError(Position location, ParserError error) {
        Location = location;
        Error = error;
    }

    public LocatedParserError WithExpected(ParserError newError) => new(Location, newError);

    public string Show(string source) {
        var spacesSB = new StringBuilder();
        var lineLength = 0;
        for (int ii = Location.IndexOfLineStart; ii < Location.Index; ++ii)
            spacesSB.Append(source[ii] == '\t' ? '\t' : '.');
        for (; Location.IndexOfLineStart + lineLength < source.Length && 
               source[Location.IndexOfLineStart + lineLength] != '\n'; ++lineLength) { }
        return $"Error at {Location.ToString()}:\n" +
               $"{source.Substring(Location.IndexOfLineStart, lineLength)}\n" +
               $"{spacesSB.ToString()}^\n" +
               $"{Error.Flatten().ToString()}";
    }

    public bool Equals(LocatedParserError other) => this == other;
    public override bool Equals(object? obj) => obj is LocatedParserError other && Equals(other);
    public override int GetHashCode() => (Location, Error).GetHashCode();
    public static bool operator ==(LocatedParserError a, LocatedParserError b) => a.Tuple == b.Tuple;
    public static bool operator !=(LocatedParserError a, LocatedParserError b) => !(a == b);
}
}