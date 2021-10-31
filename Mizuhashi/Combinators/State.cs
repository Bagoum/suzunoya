using System;

namespace Mizuhashi {
public static partial class Combinators {

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
}
}