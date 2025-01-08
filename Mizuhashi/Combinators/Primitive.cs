using System;
using System.Collections.Generic;
using System.Reactive;
using BagoumLib;
using BagoumLib.Functional;

namespace Mizuhashi {
public static partial class Combinators {
    /// <summary>
    /// A parser that always succeeds with no consumption and returns a fixed value. (FParsec preturn)
    /// </summary>
    public static Parser<T, R> PReturn<T, R>(R val) => input =>
        new ParseResult<R>(new(val), null as LocatedParserError?, input.Index, input.Index);

    /// <summary>
    /// Apply `p`, and if it succeeds, return the fixed value `val`. (FParsec >>%)
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
    /// Run two parsers in sequence. If both succeed, return the result from the first parser. (FParsec .>>)
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
    /// Run two parsers in sequence. If both succeed, return the result from the second parser. (FParsec >>.)
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
    /// Run two parsers in sequence. If both succeed, return both results as a tuple. (FParsec .>>.)
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
    /// Run three parsers in sequence. If all succeed, return the result from the middle parser. (FParsec between)
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
    /// Parse the outer parser, then the middle parser, then the outer parser again.
    /// If all succeed, return the result from the middle parser.
    /// </summary>
    public static Parser<T, B> Between<T, A, B>(this Parser<T, A> outer, Parser<T, B> middle) => Between(outer, middle, outer);

    /// <summary>
    /// FParsec &lt;?&gt;: Wrap any error messages in a label, but only if they do not consume text.
    /// </summary>
    public static Parser<T, R> Label<T, R>(this Parser<T, R> p, string label) => input => {
        var res = p(input);
        if (res.Consumed)
            return res;
        return new(res.Result, 
            res.Error.Try(out var e) ? e.WithError(new ParserError.Labelled(label, e.Error)) : null, res.Start, res.End);
    };
    
    /// <summary>
    /// FParsec &lt;??&gt;: Wrap any error messages in a label.
    /// </summary>
    public static Parser<T, R> LabelV<T, R>(this Parser<T, R> p, string label) => input => {
        var res = p(input);
        if (res.Error.Try(out var e))
            return new(res.Result, e.WithError(new ParserError.Labelled(label, e.Error)), res.Start, res.End);
        return res;
    };

    private static readonly ParserError anyErr = new ParserError.Expected("any token");
    /// <summary>
    /// Match any token (except end-of-file).
    /// </summary>
    public static Parser<T, T> Any<T>() =>
        input => input.Empty ?
            new(anyErr, input.Index) :
            new(new(input.Next), null as LocatedParserError?, input.Index, input.Step(1));


    /// <summary>
    /// Execute the first parser. If it fails non-fatally, execute the second parser instead. FParsec &lt;|&gt;
    /// </summary>
    public static Parser<T, R> Or<T, R>(this Parser<T, R> p, Parser<T, R> other) => input => {
        var result = p(input);
        if (result.Status == ResultStatus.ERROR)
            return other(input).WithPreceding(in result);
        else //success or fatal
            return result;
    };
    
    /// <summary>
    /// Execute the first parser. If it fails non-fatally, execute the second parser instead.
    /// If either succeeds, return an <see cref="BagoumLib.Functional.Either{L,R}"/> indicating which was used and its result.
    /// </summary>
    public static Parser<T, Either<R1, R2>> Either<T, R1, R2>(Parser<T, R1> left, Parser<T, R2> right) => input => {
        var result = left(input);
        if (result.Status == ResultStatus.ERROR)
            return right(input).FMap<Either<R1, R2>>(x => x).WithPreceding(in result);
        else //success or fatal
            return result.FMap<Either<R1, R2>>(x => x);
    };
    
    /// <summary>
    /// Parse one of the three provided parsers, returning an <see cref="OneOf{A,B,C}"/>
    ///  indicating which was used and its result.
    /// </summary>
    public static Parser<T, OneOf<R1,R2,R3>> OneOf<T,R1,R2,R3>(Parser<T, R1> a, Parser<T, R2> b, Parser<T, R3> c) => input => {
        var ra = a(input);
        if (ra.Status != ResultStatus.ERROR) //success or fatal
            return ra.FMap<OneOf<R1,R2,R3>>(x => x);
        var rb = b(input).WithPreceding(in ra);
        if (rb.Status != ResultStatus.ERROR)
            return rb.FMap<OneOf<R1,R2,R3>>(x => x);
        return c(input).WithPreceding(in ra).FMap<OneOf<R1, R2, R3>>(x => x);
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

    /// <summary>
    /// Non-generic base class for <see cref="PipeChoiceEntry{T,A,C}"/>.
    /// </summary>
    public abstract record PipeChoiceEntry<T, A, C> {
        internal abstract bool Apply(ParseResult<C> prev, A prevVal, InputStream<T> stream, out ParseResult<C> next);
    }

    /// <summary>
    /// A data structure that allows piping the result of a common parser into intermediary parsers of multiple types,
    /// before finalizing with a common type result.
    /// </summary>
    /// <param name="Parser">Intermediary parser.</param>
    /// <param name="Mapper">Function mapping common result and intermediary result to final result.</param>
    /// <typeparam name="T">Token type of stream.</typeparam>
    /// <typeparam name="A">Type of common parser result.</typeparam>
    /// <typeparam name="B">Type of intermediary result.</typeparam>
    /// <typeparam name="C">Common type of final result.</typeparam>
    public record PipeChoiceEntry<T, A, B, C>(Parser<T, B> Parser, Func<A, B, C> Mapper) : PipeChoiceEntry<T, A, C> {
        internal override bool Apply(ParseResult<C> prev, A prevVal, InputStream<T> stream, out ParseResult<C> next) {
            var n = Parser(stream).WithPreceding(prev);
            if (n.Status == ResultStatus.OK) {
                next = new(Mapper(prevVal, n.Result.Value), n.Error, n.Start, n.End);
                return true;
            } else {
                next = n.CastFailure<C>();
                return n.Status == ResultStatus.FATAL;
            }
        }
    }
    
    /// <summary>
    /// Given a list of parsers `ps` that use different intermediate types but combine with `first` to
    ///  produce a common final type, parse one of the list of parsers. If no parsers are provided, always fails.
    /// </summary>
    public static Parser<T, C> PipeChoice<T, A, C>(this Parser<T, A> first, params PipeChoiceEntry<T, A, C>[] ps) {
        if (ps.Length == 0) return Error<T, C>("No choice arms");
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
    
    /// <summary>
    /// <see cref="Choice{T,R}"/> with a specific label used for errors if no parser succeeds. (FParsec choiceL)
    /// </summary>
    public static Parser<T, R> ChoiceL<T, R>(string label, params Parser<T, R>[] ps) {
        if (ps.Length == 0) return Error<T, R>("No choice arms");
        var perr = new ParserError.Labelled(label, new(null as ParserError));
        return input => {
            ParseResult<R> result = default!;
            for (int ii = 0; ii < ps.Length; ++ii) {
                result = ps[ii](input);
                if (result.Status != ResultStatus.ERROR)
                    return result;
            }
            return new(result.Result, new LocatedParserError(result.Start, result.End, perr), result.Start, result.End);
        };
    }

    /// <summary>
    /// Try to parse an object of type R, and return it as type Maybe&lt;R&gt;.
    /// If it fails non-fatally, then succeed with Maybe.None. (FParsec opt)
    /// </summary>
    public static Parser<T, Maybe<R>> Opt<T, R>(this Parser<T, R> p) => input => {
        var result = p(input);
        return result.Result.Try(out var res) ? 
            new ParseResult<Maybe<R>>(Maybe<R>.Of(res), result.Error, result.Start, result.End) : 
            result.Status == ResultStatus.ERROR ?
                new ParseResult<Maybe<R>>(Maybe<R>.None, result.Error, result.Start, result.End) :
                result.CastFailure<Maybe<R>>();
    };
    
    /// <inheritdoc cref="Opt{T,R}"/>
    public static Parser<T, R?> OptN<T, R>(this Parser<T, R> p) where R : struct => input => {
        var result = p(input);
        return result.Result.Try(out var res) ? 
            new ParseResult<R?>(res, result.Error, result.Start, result.End) : 
            result.Status == ResultStatus.ERROR ?
                new ParseResult<R?>(Maybe<R?>.Of(null), result.Error, result.Start, result.End) :
                result.CastFailure<R?>();
    };
    
    /// <summary>
    /// Try to parse an object of type R. If it fails non-fatally, then succeed. (FParsec optional)
    /// </summary>
    public static Parser<T, Unit> Optional<T, R>(this Parser<T, R> p) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.FATAL => result.CastFailure<Unit>(),
            _ => new ParseResult<Unit>(new(Unit.Default), result.Error, result.Start, result.End)
        };
    };

    /// <summary>
    /// Try to parse an object of type R. If it fails non-fatally, then return the default. (FParsec &lt;|&gt;?)
    /// </summary>
    public static Parser<T, R> OptionalOr<T, R>(this Parser<T, R> p, R pret) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.ERROR => new ParseResult<R>(new(pret), result.Error, result.Start,
                result.End),
            _ => result
        };
    };

    private static readonly ParserError notEmptyErr = new ParserError.Expected("non-empty parse result");
    
    /// <summary>
    /// Run a parser. If it succeeds without consuming input, then fail, otherwise return its result. (FParsec notEmpty)
    /// </summary>
    public static Parser<T, R> NotEmpty<T, R>(this Parser<T, R> p) => input => {
        var result = p(input);
        return result.Status == ResultStatus.OK ?
            result.Consumed ?
                result :
                new(Maybe<R>.None, input.MakeError(notEmptyErr),
                    result.Start, result.End) :
            result;
    };
}
}