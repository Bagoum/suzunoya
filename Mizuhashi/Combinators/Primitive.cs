using System;
using System.Collections.Generic;
using System.Reactive;
using BagoumLib.Functional;

namespace Mizuhashi {
public static partial class Combinators {
    /// <summary>
    /// FParsec preturn
    /// </summary>
    public static Parser<R, S> PReturn<R, S>(R val) => input => 
        new ParseResult<R, S>(new(val), null, input, input);

    public static Parser<Unit, S> Ignore<S>() => PReturn<Unit, S>(Unit.Default);

    /// <summary>
    /// FParsec .>>
    /// </summary>
    public static Parser<A, S> ThenIg<A, B, S>(this Parser<A, S> first, Parser<B, S> second) =>
        inp => first(inp).BindThenReturnThis(second);
    
    /// <summary>
    /// FParsec >>.
    /// </summary>
    public static Parser<B, S> IgThen<A, B, S>(this Parser<A, S> first, Parser<B, S> second) =>
        inp => first(inp).BindThenReturnNext(second);
    
    /// <summary>
    /// FParsec .>>.
    /// </summary>
    public static Parser<(A a, B b), S> Then<A, B, S>(this Parser<A, S> first, Parser<B, S> second) =>
        inp => first(inp).BindThenReturnTuple(second);

    /// <summary>
    /// FParsec between
    /// </summary>
    public static Parser<B, S> Between<A, B, C, S>(this Parser<A, S> left, Parser<B, S> middle, Parser<C, S> right) =>
        inp => left(inp).BindThenReturnMiddle(middle, right);

    public static Parser<B, S> Between<A, B, S>(this Parser<A, S> outer, Parser<B, S> middle) =>
        inp => outer(inp).BindThenReturnMiddle(middle, outer);

    /// <summary>
    /// FParsec &lt;??&gt;
    /// </summary>
    public static Parser<R, S> Label<R, S>(this Parser<R, S> p, string label) => input => 
        p(input).WithWrapError(label);
    
    /// <summary>
    /// FParsec attempt
    /// </summary>
    public static Parser<R, S> Attempt<R, S>(this Parser<R, S> p) => input => {
        var result = p(input);
        return result.Status == ResultStatus.FATAL ? 
            new ParseResult<R, S>(result.Result, result.Errors, result.Previous, result.Previous) : 
            result;
    };

    /// <summary>
    /// FParsec &lt;|&gt;
    /// </summary>
    public static Parser<R, S> Or<R, S>(this Parser<R, S> p, Parser<R, S> other) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.ERROR => other(input).WithPrecedingError(result),
            _ => result
        };
    };
    
    /// <summary>
    /// FParsec choice
    /// </summary>
    public static Parser<R, S> Choice<R, S>(params Parser<R, S>[] ps) {
        if (ps.Length == 0) throw new Exception("No arms provided to choice parser");
        return input => {
            ParserError.OneOf? err = null;
            ParseResult<R, S> result = default;
            void AddError(ParserError? next) {
                if (next == null)
                    return;
                (err ??= new ParserError.OneOf(new())).Errors.Add(next);
            }
            for (int ii = 0; ii < ps.Length; ++ii) {
                result = ps[ii](input);
                AddError(result.Errors?.Error);
                if (result.Status != ResultStatus.ERROR)
                    return new(result.Result, err == null ? null : new LocatedParserError(input.Position, err), 
                        result.Previous, result.Remaining);
            }
            return new(result.Result, err == null ? null : new LocatedParserError(input.Position, err),
                result.Previous, result.Remaining);
        };
    }
    
    /// <summary>
    /// FParsec choiceL
    /// </summary>
    public static Parser<R, S> ChoiceL<R, S>(string label, params Parser<R, S>[] ps) {
        if (ps.Length == 0) throw new Exception("No arms provided to choice parser");
        var perr = new ParserError.Labelled(label, new("Couldn't parse any of the arms."));
        return input => {
            var err = new LocatedParserError(input.Position, perr);
            ParseResult<R, S> result = default;
            for (int ii = 0; ii < ps.Length; ++ii) {
                result = ps[ii](input);
                if (result.Status != ResultStatus.ERROR)
                    return new(result.Result, err, result.Previous, result.Remaining);
            }
            return new(result.Result, err, result.Previous, result.Remaining);
        };
    }

    /// <summary>
    /// FParsec opt
    /// </summary>
    public static Parser<R?, S> Opt<R, S>(this Parser<R, S> p) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.ERROR => new ParseResult<R?, S>(Maybe<R?>.None, result.Errors, result.Previous,
                result.Remaining),
            ResultStatus.OK => new ParseResult<R?, S>(result.Result.Value, result.Errors, result.Previous,
                result.Remaining),
            _ => result.ForwardError<R?>()
        };
    };
    
    /// <summary>
    /// FParsec optional
    /// </summary>
    public static Parser<Unit, S> Optional<R, S>(this Parser<R, S> p) => input => {
        var result = p(input);
        return result.Status switch {
            ResultStatus.ERROR => new ParseResult<Unit, S>(Maybe<Unit>.None, result.Errors, result.Previous,
                result.Remaining),
            ResultStatus.OK => new ParseResult<Unit, S>(new(Unit.Default), result.Errors, result.Previous,
                result.Remaining),
            _ => result.ForwardError<Unit>()
        };
    };

    /// <summary>
    /// FParsec notEmpty
    /// </summary>
    public static Parser<R, S> NotEmpty<R, S>(this Parser<R, S> p) => input => {
        var result = p(input);
        return result.Status == ResultStatus.OK ?
            result.Consumed ?
                result :
                new(Maybe<R>.None, input.MakeError(new ParserError.Expected("non-empty parse result")),
                    result.Previous, result.Remaining) :
            result;
    };

    /// <summary>
    /// Applies a parser repeatedly until it errors, and returns a list of all results.
    /// <br/>Note that if the parser errors fatally, this will error fatally as well.
    /// <br/>To avoid infinite recursion, if the parser succeeds without consumption, this will fail.
    /// <br/>FParsec many
    /// </summary>
    /// <param name="p">Parser to apply repeatedly</param>
    /// <param name="atleastOne">If true, then will require at least one result</param>
    /// <returns></returns>
    public static Parser<List<R>, S> Many<R, S>(this Parser<R, S> p, bool atleastOne=false) => input => {
        var results = new List<R>();
        var nextInput = input;
        while (true) {
            var next = p(nextInput);
            if (next.Status == ResultStatus.FATAL)
                return next.ForwardError<List<R>>();
            else if (next.Status == ResultStatus.ERROR)
                return (results.Count == 0 && atleastOne) ?
                    new(new ParserError.Expected("at least one value for Many parser"), input) :
                    new(results, next.Errors, input, next.Remaining);
            else if (!next.Consumed)
                return new(
                    new ParserError.Failure("Many parser parsed an object without consuming text."), next.Previous);
            else
                results.Add(next.Result.Value);
            nextInput = next.Remaining;
        }
    };

    public static Parser<List<R>, S> Many1<R, S>(this Parser<R, S> p) => Many(p, true);
    
    
    /// <summary>
    /// Applies a parser repeatedly until it errors.
    /// <br/>Note that if the parser errors fatally, this will error fatally as well.
    /// <br/>To avoid infinite recursion, if the parser succeeds without consumption, this will fail.
    /// <br/>FParsec many
    /// </summary>
    /// <param name="p">Parser to apply repeatedly</param>
    /// <param name="atleastOne">If true, then will require at least one result</param>
    /// <returns></returns>
    public static Parser<Unit, S> SkipMany<R, S>(this Parser<R, S> p, bool atleastOne=false) => input => {
        bool foundOne = false;
        var nextInput = input;
        while (true) {
            var next = p(nextInput);
            if (next.Status == ResultStatus.FATAL)
                return next.ForwardError<Unit>();
            else if (next.Status == ResultStatus.ERROR)
                return (!foundOne && atleastOne) ?
                    new(new ParserError.Expected("at least one value for SkipMany parser"), input) :
                    new(Unit.Default, next.Errors, input, next.Remaining);
            else if (!next.Consumed)
                return new(
                    new ParserError.Failure("SkipMany parser parsed an object without consuming text."), next.Previous);
            else
                foundOne = true;
            nextInput = next.Remaining;
        }
    };

    public static Parser<Unit, S> SkipMany1<R, S>(this Parser<R, S> p) => SkipMany(p, true);


    /// <summary>
    /// Parse `p (sep p)*`. If atleastOne is false, then allows parsing nothing.
    /// <br/>FParsec sepBy
    /// </summary>
    public static Parser<List<R>, S> SepBy<R, U, S>(this Parser<R, S> ele, Parser<U, S> sep, bool atleastOne = false) =>
        input => {
            var results = new List<R>();
            var next = ele(input);
            if (next.Status == ResultStatus.FATAL)
                return next.ForwardError<List<R>>();
            else if (next.Status == ResultStatus.ERROR)
                return atleastOne ?
                    next.ForwardError<List<R>>() :
                    new(results, next.Errors, input, next.Remaining);
            else
                results.Add(next.Result.Value);

            var nextInput = next.Remaining;
            while (true) {
                var sepParsed = sep(nextInput);
                if (sepParsed.Status == ResultStatus.FATAL)
                    return sepParsed.ForwardError<List<R>>();
                else if (sepParsed.Status == ResultStatus.ERROR)
                    return new(results, next.Errors, input, next.Remaining);

                next = ele(sepParsed.Remaining);
                if (next.Status == ResultStatus.OK)
                    results.Add(next.Result.Value);
                else
                    return next.ForwardError<List<R>>();
                nextInput = next.Remaining;
            }
        };

    public static Parser<List<R>, S> SepBy1<R, U, S>(this Parser<R, S> ele, Parser<U, S> sep) => SepBy(ele, sep, true);

}
}