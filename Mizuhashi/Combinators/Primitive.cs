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
    public static Parser<R> PReturn<R>(R val) => input => 
        new ParseResult<R>(new(val), null, input.Index, input.Index);

    /// <summary>
    /// PReturn(Unit.Default)
    /// </summary>
    public static Parser<Unit> Ignore() => PReturn(Unit.Default);

    /// <summary>
    /// Fails with the given failure string.
    /// </summary>
    public static Parser<R> Fail<R>(string reason) => input => 
        new ParseResult<R>(new ParserError.Failure(reason), input.Index, input.Index + 1);

    /// <summary>
    /// FParsec .>>
    /// </summary>
    public static Parser<A> ThenIg<A, B>(this Parser<A> first, Parser<B> second) => input => {
        var rx = first(input);
        if (rx.Result.Valid) {
            var ry = second(input);
            return new(rx.Result, rx.MergeErrors(in ry), rx.Start, ry.End);
        } else
            return rx;
    };
    
    /// <summary>
    /// FParsec >>.
    /// </summary>
    public static Parser<B> IgThen<A, B>(this Parser<A> first, Parser<B> second) => input => {
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
    public static Parser<(A a, B b)> Then<A, B>(this Parser<A> first, Parser<B> second) => input => {
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
    public static Parser<B> Between<A, B, C>(this Parser<A> left, Parser<B> middle, Parser<C> right) => input => {
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
    public static Parser<B> Between<A, B>(this Parser<A> outer, Parser<B> middle) => Between(outer, middle, outer);

    /// <summary>
    /// FParsec &lt;??&gt;
    /// </summary>
    public static Parser<R> Label<R>(this Parser<R> p, string label) => input => 
        p(input).WithWrapError(label);
    
    /// <summary>
    /// FParsec attempt
    /// </summary>
    public static Parser<R> Attempt<R>(this Parser<R> p) => input => {
        var state = input.Stative;
        var result = p(input);
        if (result.Status == ResultStatus.FATAL) {
            input.Rollback(state);
            return new(result.Result, result.Error, result.Start, result.Start);
        } else
            return result;
    };

    /// <summary>
    /// FParsec &lt;|&gt;
    /// </summary>
    public static Parser<R> Or<R>(this Parser<R> p, Parser<R> other) => input => {
        var state = input.Stative;
        var result = p(input);
        return result.Status switch {
            ResultStatus.ERROR => other(input).WithPreceding(in result),
            _ => result
        };
    };
    
    /// <summary>
    /// FParsec choice
    /// </summary>
    public static Parser<R> Choice<R>(params Parser<R>[] ps) {
        if (ps.Length == 0) throw new Exception("No arms provided to choice parser");
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
    public static Parser<R> ChoiceL<R>(string label, params Parser<R>[] ps) {
        if (ps.Length == 0) throw new Exception("No arms provided to choice parser");
        var perr = new ParserError.Labelled(label, new("Couldn't parse any of the arms."));
        return input => {
            var err = new LocatedParserError(input.Index, perr);
            ParseResult<R> result = default!;
            for (int ii = 0; ii < ps.Length; ++ii) {
                result = ps[ii](input);
                if (result.Status != ResultStatus.ERROR)
                    return new(result.Result, err, result.Start, result.End);
            }
            return new(result.Result, err, result.Start, result.End);
        };
    }

    /// <summary>
    /// FParsec opt
    /// </summary>
    public static Parser<R?> Opt<R>(this Parser<R> p) where R: struct => input => {
        var result = p(input);
        return result.Status == ResultStatus.ERROR ? 
            new ParseResult<R?>(new(null), result.Error, result.Start, result.End) : 
            result.FMap<R?>(x => x);
    };
    
    /// <summary>
    /// FParsec optional
    /// </summary>
    public static Parser<Unit> Optional<R>(this Parser<R> p) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.FATAL => result.CastFailure<Unit>(),
            _ => new ParseResult<Unit>(new(Unit.Default), result.Error, result.Start, result.End)
        };
    };

    /// <summary>
    /// FParsec &lt;|&gt;?
    /// </summary>
    public static Parser<R> OptionalOr<R>(this Parser<R> p, R pret) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.ERROR => new ParseResult<R>(new(pret), result.Error, result.Start,
                result.End),
            _ => result
        };
    };

    /// <summary>
    /// FParsec notEmpty
    /// </summary>
    public static Parser<R> NotEmpty<R>(this Parser<R> p) => input => {
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