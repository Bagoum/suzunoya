using System;

namespace Mizuhashi {
public static partial class Combinators {

    /// <summary>
    /// FParsec getUserState
    /// </summary>
    public static Parser<S, S> GetState<S>() => 
        inp => new(new(inp.State), null, inp, inp);
    
    /// <summary>
    /// FParsec updateUserState
    /// </summary>
    public static Parser<S, S> UpdateState<S>(Func<S, S> updater) => 
        inp => new(new(updater(inp.State)), null, inp, inp);

    /// <summary>
    /// FParsec getPosition
    /// </summary>
    public static Parser<Position, S> GetPosition<S>() =>
        inp => new(new(inp.Position), null, inp, inp);
}
}