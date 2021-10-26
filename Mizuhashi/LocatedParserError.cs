using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace Mizuhashi {
public abstract record ParserError {
    protected virtual IEnumerable<ParserError> Enumerated() => new[]{this};
    public virtual ParserError JoinWith(ParserError other) => new EitherOf(this, other);

    public abstract string Show(string s);

    public record Failure(string Message) : ParserError {
        public override string Show(string s) => $"Failure: {Message}";
    }

    public record ExpectedChar(char Char) : ParserError {
        public override string Show(string s) => $"Expected '{Char}'";
    }
    public record Expected(string String) : ParserError { 
        public override string Show(string s) => $"Expected {String}";
    }

    public record IncorrectNumber : ParserError {
        public int Required { get; }
        public int Received { get; }
        public string? ObjectDescription { get; }
        public LocatedParserError? Inner { get; }

        public IncorrectNumber(int Required, int Received, string? ObjectDescription, LocatedParserError? Inner) {
            this.Required = Required;
            this.Received = Received;
            this.ObjectDescription = ObjectDescription;
            this.Inner = Inner;
        }

        public override string Show(string s) {
            var objSuffix = ObjectDescription == null ? "" : $" of {ObjectDescription}";
            var excSuffix = Inner == null ? "" : "\n\t" + Inner?.Show(s).Replace("\n", "\n\t");
            return $"Required {Required} values{objSuffix}, but only received {Received}." + excSuffix;
        }
    }
    public record UnexpectedChar(char Char) : ParserError { 
        public override string Show(string s) => $"Did not expect '{Char}'";
        
    }
    public record Unexpected(string String) : ParserError { 
        public override string Show(string s) => $"Did not expect {String}";
    }
    
    public record Labelled(string Label, Either<string, LocatedParserError?> ExpectedContent) : ParserError {
        public override string Show(string s) => ExpectedContent.Map(
            err => $"Failed in parsing {Label}: {err}",
            err => $"Failed in parsing {Label}" + (err == null ?
                "" :
                ":\n\t" + err.Value.Show(s).Replace("\n", "\n\t"))
        );
    }

    public record EitherOf(ParserError OptionA, ParserError OptionB) : ParserError {
        protected override IEnumerable<ParserError> Enumerated() => OptionA.Enumerated().Concat(OptionB.Enumerated());
        
        public override string Show(string s) {
            return "This should not appear.";
        }
    }

    public record OneOf(List<ParserError> Errors) : ParserError {
        protected override IEnumerable<ParserError> Enumerated() => Errors.SelectMany(e => e.Enumerated());
        public override ParserError JoinWith(ParserError other) {
            Errors.Add(other);
            return this;
        }

        public override string Show(string s) {
            if (Errors.Count == 1)
                return Errors[0].Show(s);
            return "Parsing failed. Resolve any of the following errors to continue.\n\t" + string.Join("\n",
                Errors.Select(e => e.Show(s))).Replace("\n", "\n\t");
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
    //Store Index instead of position because it's more space-efficient. We can expand back to position if there are parsing errors
    public readonly int Index;
    public readonly ParserError Error;

    private (int, ParserError) Tuple => (Index, Error);

    public LocatedParserError(int index, ParserError error) {
        Index = index;
        Error = error;
    }

    public string Show(string source) {
        var spacesSB = new StringBuilder();
        var lineLength = 0;
        var pos = new Position(source, Index);
        for (int ii = pos.IndexOfLineStart; ii < pos.Index; ++ii)
            spacesSB.Append(source[ii] == '\t' ? '\t' : '.');
        for (; pos.IndexOfLineStart + lineLength < source.Length && 
               source[pos.IndexOfLineStart + lineLength] != '\n'; ++lineLength) { }
        return $"Error at {pos.ToString()}:\n" +
               $"{source.Substring(pos.IndexOfLineStart, lineLength)}\n" +
               $"{spacesSB.ToString()}^\n" +
               $"{Error.Flatten().Show(source)}";
    }
    
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
    public bool Equals(LocatedParserError other) => this == other;
    public override bool Equals(object? obj) => obj is LocatedParserError other && Equals(other);
    public override int GetHashCode() => (Location: Index, Error).GetHashCode();
    public static bool operator ==(LocatedParserError a, LocatedParserError b) => a.Tuple == b.Tuple;
    public static bool operator !=(LocatedParserError a, LocatedParserError b) => !(a == b);
}
}