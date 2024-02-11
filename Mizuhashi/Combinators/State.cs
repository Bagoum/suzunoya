using System;

namespace Mizuhashi {
public static partial class Combinators {
    /// <summary>
    /// Return the input stream that is currently being parsed.
    /// </summary>
    public static Parser<T, InputStream<T>> GetStream<T>() => inp => new(inp, null, inp.Index, inp.Index);

    /// <summary>
    /// FParsec getUserState
    /// </summary>
    public static Parser<T, S> GetState<T, S>() => 
        inp => inp.Stative.State is S state ?
            new(state, null, inp.Index, inp.Index) :
            new(new ParserError.Failure(
                $"Expected state variable of type {typeof(S)}, but received {inp.Stative.State.GetType()}"), inp.Index);

    /// <summary>
    /// FParsec setUserState
    /// </summary>
    public static Parser<T, S> SetState<T, S>(S state) =>
        inp => {
            inp.UpdateState(state!);
            return new(new(state), null, inp.Index, inp.Index);
        };
    
    /// <summary>
    /// FParsec updateUserState
    /// </summary>
    public static Parser<T, S> UpdateState<T, S>(Func<S, S> updater) => 
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
    /// Get the current index of the stream.
    /// </summary>
    public static Parser<T, int> GetIndex<T>() =>
        inp => new(new(inp.Index), null, inp.Index, inp.Index);
    
    /// <summary>
    /// FParsec getPosition
    /// </summary>
    public static readonly Parser<char, Position> GetPosition =
        inp => new(inp.Stative.SourcePosition, null, inp.Index, inp.Index);

    /// <summary>
    /// Take the position information (<see cref="int"/> start and (exclusive) end) from the parse
    ///  result and put it in a condensed type.
    /// </summary>
    public static Parser<T, U> WrapPositionI<T, U>(this Parser<T, T> p, Func<int, T, int, U> condense) =>
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
    public static Parser<char, U> WrapPosition<T, U>(this Parser<char, T> p, Func<T, PositionRange, U> condense) =>
        inp => {
            var start = inp.Stative.SourcePosition;
            var res = p(inp);
            return res.Result.Try(out var x) ? 
                new ParseResult<U>(condense(x, new(start, inp.Stative.SourcePosition)), 
                    res.Error, res.Start, res.End) :
                res.CastFailure<U>();
        };
}
}