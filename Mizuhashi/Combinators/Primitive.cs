using System;
using System.Collections.Generic;
using System.Reactive;
using BagoumLib;
using BagoumLib.Functional;

namespace Mizuhashi {
public static partial class Combinators {
    /// <summary>
    /// FParsec preturn
    /// </summary>
    public static Parser<T, R> PReturn<T, R>(R val) => input =>
        new ParseResult<R>(new(val), null, input.Index, input.Index);

    /// <summary>
    /// FParsec >>%
    /// </summary>
    public static Parser<T, B> ThenPReturn<T, A, B>(this Parser<T, A> p, B val) => input => {
        var result = p(input);
        return new(result.Result.Valid ? Maybe<B>.Of(val) : Maybe<B>.None,
            result.Error, result.Start, result.End);
    };

    /// <summary>
    /// PReturn(Unit.Default)
    /// </summary>
    public static Parser<T, Unit> Ignore<T>() => PReturn<T, Unit>(Unit.Default);

    /// <summary>
    /// Run the given parser, then ignore its output.
    /// </summary>
    public static Parser<T, Unit> Ignore<T, R>(this Parser<T, R> p) => input => {
        var result = p(input);
        return new(result.Result.Valid ? Maybe<Unit>.Of(default) : Maybe<Unit>.None,
            result.Error, result.Start, result.End);
    };

    /// <summary>
    /// Fails (fatally) with the given failure string.
    /// <br/>Note: this consumes a character and constructs a fatal error.
    /// </summary>
    public static Parser<T, R> Fail<T, R>(string reason) => Fail<T, R>(new ParserError.Failure(reason));
   
    /// <inheritdoc cref="Fail{T,R}(string)"/>
    public static Parser<T, R> Fail<T, R>(ParserError reason) => 
        input => new ParseResult<R>(reason, input.Index, input.Index + 1);

    /// <inheritdoc cref="Error{T,R}(ParserError)"/>
    public static Parser<T, R> Error<T, R>(string reason) => Error<T, R>(new ParserError.Failure(reason));
    
    /// <summary>
    /// Fails (non-fatally) with the given failure string.
    /// <br/>Note: this does not consume, and therefore constructs a non-fatal error.
    /// </summary>
    public static Parser<T, R> Error<T, R>(ParserError reason) => input => 
        new ParseResult<R>(reason, input.Index);

    /// <summary>
    /// If the provided value is R, then <see cref="PReturn{T,R}"/> it;
    /// else <see cref="Error{T,R}(ParserError)"/> it.
    /// </summary>
    public static Parser<T, R> ReturnOrError<T, R>(Either<R, ParserError> result) =>
        result.IsLeft ? PReturn<T, R>(result.Left) : Error<T, R>(result.Right);

    /// <summary>
    /// FParsec .>>
    /// </summary>
    public static Parser<T, A> ThenIg<T, A, B>(this Parser<T, A> first, Parser<T, B> second) => input => {
        var rx = first(input);
        if (rx.Result.Valid) {
            var ry = second(input);
            return new(ry.Result.Valid ? rx.Result : Maybe<A>.None, rx.MergeErrors(in ry), rx.Start, ry.End);
        } else
            return rx;
    };
    
    /// <summary>
    /// FParsec >>.
    /// </summary>
    public static Parser<T, B> IgThen<T, A, B>(this Parser<T, A> first, Parser<T, B> second) => input => {
        var rx = first(input);
        if (rx.Result.Valid) {
            var ry = second(input);
            return new(ry.Result, rx.MergeErrors(in ry), rx.Start, ry.End);
        } else
            return rx.CastFailure<B>();
    };
    
    /// <summary>
    /// FParsec .>>.
    /// </summary>
    public static Parser<T, (A a, B b)> Then<T, A, B>(this Parser<T, A> first, Parser<T, B> second) => input => {
        var rx = first(input);
        if (rx.Result.Try(out var x)) {
            var ry = second(input);
            return new(ry.Result.Try(out var y) ? new((x, y)) : Maybe<(A, B)>.None, 
                rx.MergeErrors(in ry), rx.Start, ry.End);
        } else
            return rx.CastFailure<(A, B)>();
    };

    /// <summary>
    /// FParsec between
    /// </summary>
    public static Parser<T, B> Between<T, A, B, C>(this Parser<T, A> left, Parser<T, B> middle, Parser<T, C> right) => input => {
        var rx = left(input);
        if (!rx.Result.Valid) 
            return rx.CastFailure<B>();
        var ry = middle(input).WithPreceding(in rx);
        if (!ry.Result.Try(out var value))
            return ry;
        var rz = right(input).WithPreceding(in ry);
        if (!rz.Result.Valid) 
            return rz.CastFailure<B>();
        return new(value, rz.Error, rz.Start, rz.End);
    };

    /// <summary>
    /// Parse the outer parser, then the middle parse, then the outer parser again, and return
    /// the result from the middle parser.
    /// </summary>
    public static Parser<T, B> Between<T, A, B>(this Parser<T, A> outer, Parser<T, B> middle) => Between(outer, middle, outer);

    /// <summary>
    /// FParsec &lt;??&gt;
    /// </summary>
    public static Parser<T, R> Label<T, R>(this Parser<T, R> p, string label) => input => 
        p(input).WithWrapError(label);

    /// <summary>
    /// Match any token (except end-of-file).
    /// </summary>
    public static Parser<T, T> Any<T>() {
        var err = new ParserError.Expected("any token");
        return input => input.Empty ?
            new(err, input.Index) :
            new(new(input.Next), null, input.Index, input.Step(1));
    }
    
    
    /// <summary>
    /// FParsec &lt;|&gt;
    /// </summary>
    public static Parser<T, R> Or<T, R>(this Parser<T, R> p, Parser<T, R> other) => input => {
        var result = p(input);
        if (result.Status == ResultStatus.ERROR)
            return other(input).WithPreceding(in result);
        else //success or fatal
            return result;
    };
    
    /// <summary>
    /// Parse any of the provided parsers. If no parsers are provided, always fails.
    /// </summary>
    public static Parser<T, R> Choice<T, R>(params Parser<T, R>[] ps) {
        if (ps.Length == 0) return Error<T, R>("No choice arms");
        return input => {
            ParseResult<R>? result = default;
            for (int ii = 0; ii < ps.Length; ++ii) {
                result = (result.HasValue) ? ps[ii](input).WithPreceding(result.Value) : ps[ii](input);
                if (result.Value.Status != ResultStatus.ERROR)
                    return result.Value;
            }
            return result ?? default;
        };
    }
/*
    public abstract record PipeChoiceEntry<A, C> {
        public abstract bool Apply(ParseResult<C> prev, A prevVal, InputStream stream, out ParseResult<C> next);
    }

    public record PipeChoiceEntry<A, B, C>(Parser<B> Parser, Func<A, B, Maybe<C>> Mapper) : PipeChoiceEntry<A, C> {
        public override bool Apply(ParseResult<C> prev, A prevVal, InputStream stream, out ParseResult<C> next) {
            var n = Parser(stream).WithPreceding(prev);
            if (n.Status == ResultStatus.OK) {
                next = new(Mapper(prevVal, n.Result.Value), n.Error, n.Start, n.End);
                return true;
            } else {
                next = n.CastFailure<C>();
                return n.Status == ResultStatus.ERROR;
            }
        }
    }
    
    public static Parser<C> PipeChoice<A, C>(this Parser<A> first, params PipeChoiceEntry<A, C>[] ps) {
        if (ps.Length == 0) throw new Exception("No arms provided to choice parser");
        return input => {
            var rx = first(input);
            if (!rx.Result.Try(out var x))
                return rx.CastFailure<C>();
            ParseResult<C> result = rx.FMap<C>(_ => default!);
            for (int ii = 0; ii < ps.Length; ++ii)
                if (ps[ii].Apply(result, x, input, out result))
                    return result;
            return result;
        };
    }
    */
    
    /// <summary>
    /// FParsec choiceL
    /// </summary>
    public static Parser<T, R> ChoiceL<T, R>(string label, params Parser<T, R>[] ps) {
        if (ps.Length == 0) return Error<T, R>("No choice arms");
        var perr = new ParserError.Labelled(label, new("Couldn't parse any of the arms."));
        return input => {
            var err = new LocatedParserError(input.Index, perr);
            ParseResult<R> result = default!;
            for (int ii = 0; ii < ps.Length; ++ii) {
                result = ps[ii](input);
                if (result.Status != ResultStatus.ERROR)
                    return result;
            }
            return new(result.Result, err, result.Start, result.End);
        };
    }

    /// <summary>
    /// FParsec opt
    /// </summary>
    public static Parser<T, Maybe<R>> Opt<T, R>(this Parser<T, R> p) => input => {
        var result = p(input);
        return result.Status == ResultStatus.ERROR ? 
            new ParseResult<Maybe<R>>(Maybe<R>.None, result.Error, result.Start, result.End) : 
            result.FMap(Maybe<R>.Of);
    };
    
    /// <summary>
    /// FParsec optional
    /// </summary>
    public static Parser<T, Unit> Optional<T, R>(this Parser<T, R> p) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.FATAL => result.CastFailure<Unit>(),
            _ => new ParseResult<Unit>(new(Unit.Default), result.Error, result.Start, result.End)
        };
    };

    /// <summary>
    /// FParsec &lt;|&gt;?
    /// </summary>
    public static Parser<T, R> OptionalOr<T, R>(this Parser<T, R> p, R pret) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.ERROR => new ParseResult<R>(new(pret), result.Error, result.Start,
                result.End),
            _ => result
        };
    };

    /// <summary>
    /// Try to parse an object of type R, and return it as type Maybe&lt;R&gt;.
    /// If it fails (non-catastrophically), then succeed with Maybe.None.
    /// </summary>
    public static Parser<T, Maybe<R>> OptionalOrNone<T, R>(this Parser<T, R> p) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.ERROR => new ParseResult<Maybe<R>>(Maybe<Maybe<R>>.Of(Maybe<R>.None),
                result.Error, result.Start, result.End),
            _ => result.FMap<Maybe<R>>(x => x)
        };
    };

    /// <summary>
    /// Try to parse an object of type R, and return it as type R?.
    /// If it fails (non-catastrophically), then succeed with null(R?).
    /// </summary>
    public static Parser<T, R?> OptionalOrNull<T, R>(this Parser<T, R> p) where R : struct => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.ERROR => new ParseResult<R?>(Maybe<R?>.Of(null), 
                result.Error, result.Start, result.End),
            _ => result.FMap<R?>(x => x)
        };
    };

    /// <summary>
    /// FParsec notEmpty
    /// </summary>
    public static Parser<T, R> NotEmpty<T, R>(this Parser<T, R> p) => input => {
        var result = p(input);
        return result.Status == ResultStatus.OK ?
            result.Consumed ?
                result :
                new(Maybe<R>.None, input.MakeError(new ParserError.Expected("non-empty parse result")),
                    result.Start, result.End) :
            result;
    };
}
}