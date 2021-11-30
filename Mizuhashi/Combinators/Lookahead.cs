using System;
using System.Reactive;
using BagoumLib.Functional;

namespace Mizuhashi {
public static partial class Combinators {
    /// <summary>
    /// FParsec attempt
    /// </summary>
    public static Parser<R> Attempt<R>(this Parser<R> p) => input => {
        var state = input.Stative;
        var result = p(input);
        if (result.Status == ResultStatus.FATAL) {
            input.Rollback(state, result.ErrorOrThrow);
            return new(result.Result, result.Error, result.Start, result.Start);
        } else
            return result;
    };
    
    /// <summary>
    /// FParsec .>>?
    /// <br/>Backtracks only if the second parser hits a non-fatal error.
    /// </summary>
    public static Parser<A> ThenTryIg<A, B>(this Parser<A> first, Parser<B> second) => input => {
        var state = input.Stative;
        var i = state.Position.Index;
        var rx = first(input);
        if (rx.Result.Valid) {
            var ry = second(input);
            if (ry.Status == ResultStatus.ERROR) {
                input.Rollback(state, ry.ErrorOrThrow);
                return new(ry.Result.Valid ? rx.Result : Maybe<A>.None, rx.MergeErrors(in ry), i, i);
            }
            return new(rx.Result, rx.MergeErrors(in ry), rx.Start, ry.End);
        } else
            return rx;
    };
    
    /// <summary>
    /// FParsec >>? (which should be >>.?)
    /// <br/>Backtracks only if the second parser hits a non-fatal error.
    /// </summary>
    public static Parser<B> IgThenTry<A, B>(this Parser<A> first, Parser<B> second) => input => {
        var state = input.Stative;
        var i = state.Position.Index;
        var rx = first(input);
        if (rx.Result.Valid) {
            var ry = second(input);
            if (ry.Status == ResultStatus.ERROR) {
                input.Rollback(state, ry.ErrorOrThrow);
                return new(ry.Result, rx.MergeErrors(in ry), i, i);
            }
            return new(ry.Result, rx.MergeErrors(in ry), rx.Start, ry.End);
        } else
            return rx.CastFailure<B>();
    };
    
    /// <summary>
    /// FParsec .>>.?
    /// <br/>Backtracks only if the second parser hits a non-fatal error.
    /// </summary>
    public static Parser<(A a, B b)> ThenTry<A, B>(this Parser<A> first, Parser<B> second) => input => {
        var state = input.Stative;
        var i = state.Position.Index;
        var rx = first(input);
        if (rx.Result.Try(out var x)) {
            var ry = second(input);
            if (ry.Status == ResultStatus.ERROR) {
                input.Rollback(state, ry.Error);
                return new(Maybe<(A, B)>.None, rx.MergeErrors(in ry), i, i);
            }
            return new(ry.Result.Try(out var y) ? new((x, y)) : Maybe<(A, B)>.None, 
                rx.MergeErrors(in ry), rx.Start, ry.End);
        } else
            return rx.CastFailure<(A, B)>();
    };

    /// <summary>
    /// FParsec followedBy/followedByL. Attempts to apply the parser, but does not change state.
    /// <br/>Unlike FParsec, returns the parsed value.
    /// <br/>Also unlike FParsec, uses the error message from the parser.
    ///  It is usually a good idea to wrap this in a label for clarity.
    /// </summary>
    public static Parser<R> IsPresent<R>(this Parser<R> p) {
        return input => {
            var state = input.Stative;
            var result = p(input);
            input.Rollback(state, null);
            return new ParseResult<R>(result.Result, result.Error, result.Start, result.Start);
        };
    }
    
    /// <summary>
    /// FParsec notFollowedBy/notFollowedByL. Attempts to apply the parser, but does not change state.
    /// </summary>
    public static Parser<Unit> IsNotPresent<R>(this Parser<R> p, string? expected = null) {
        var err = new ParserError.Unexpected(expected ?? "(No description provided)");
        return input => {
            var state = input.Stative;
            var result = p(input);
            var lerr = new LocatedParserError(state.Position.Index, err);
            input.Rollback(state, null);
            return new ParseResult<Unit>(result.Result.Valid ? Maybe<Unit>.None : Maybe<Unit>.Of(default), 
                result.Error == null ? lerr : null, result.Start, result.Start);
        };
    }
    
}
}