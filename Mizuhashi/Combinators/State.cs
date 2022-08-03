using System;

namespace Mizuhashi {
public static partial class Combinators {
    public static Parser<InputStream> GetStream() => inp => new(inp, null, inp.Index, inp.Index);

    /// <summary>
    /// FParsec getUserState
    /// </summary>
    public static Parser<S> GetState<S>() => 
        inp => inp.Stative.State is S state ?
            new(state, null, inp.Index, inp.Index) :
            new(new ParserError.Failure(
                $"Expected state variable of type {typeof(S)}, but received {inp.Stative.State.GetType()}"), inp.Index);

    /// <summary>
    /// FParsec setUserState
    /// </summary>
    public static Parser<S> SetState<S>(S state) =>
        inp => {
            inp.UpdateState(state!);
            return new(new(state), null, inp.Index, inp.Index);
        };
    
    /// <summary>
    /// FParsec updateUserState
    /// </summary>
    public static Parser<S> UpdateState<S>(Func<S, S> updater) => 
        inp => {
            if (inp.Stative.State is S state) {
                var ns = updater(state);
                inp.UpdateState(ns!);
                return new(new(ns), null, inp.Index, inp.Index);
            } else
                return new(new ParserError.Failure(
                    $"Expected state variable of type {typeof(S)}, but received {inp.Stative.State.GetType()}"), inp.Index);
        };

    /// <summary>
    /// FParsec getPosition
    /// </summary>
    public static readonly Parser<Position> GetPosition =
        inp => new(new(inp.Stative.Position), null, inp.Index, inp.Index);

    /// <summary>
    /// Take the position information (<see cref="int"/> start and (exclusive) end) from the parse
    ///  result and put it in a condensed type.
    /// </summary>
    public static Parser<U> WrapPositionI<T, U>(this Parser<T> p, Func<int, T, int, U> condense) =>
        inp => {
            var res = p(inp);
            return res.Result.Try(out var x) ?
                new ParseResult<U>(condense(res.Start, x, res.End), res.Error, res.Start, res.End) :
                res.CastFailure<U>();
        };
    
    /// <summary>
    /// Take the position information (<see cref="Position"/> start and (exclusive) end) from the parse
    ///  result and put it in a condensed type.
    /// </summary>
    public static Parser<U> WrapPosition<T, U>(this Parser<T> p, Func<T, PositionRange, U> condense) =>
        inp => {
            var start = inp.Stative.Position;
            var res = p(inp);
            return res.Result.Try(out var x) ? 
                new ParseResult<U>(condense(x, new(start, inp.Stative.Position)), res.Error, res.Start, res.End) :
                res.CastFailure<U>();
        };
}
}