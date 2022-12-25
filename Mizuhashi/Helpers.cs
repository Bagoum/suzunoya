using BagoumLib.Functional;

namespace Mizuhashi {
public static class Helpers {
    /// <summary>
    /// Returns either the result of a parser, or its final failure.
    /// <br/>Does not include backtracking information, but that is stored in s.
    /// </summary>
    public static Either<R, LocatedParserError> ResultOrError<T, R>(this Parser<T, R> p, InputStream<T> s) {
        var result = p(s);
        if (result.Result.Valid)
            return new(result.Result.Value);
        return new(result.ErrorOrThrow);
    }
    
    /// <summary>
    /// Returns either the result of a parser, or its error string.
    /// <br/>The error string includes backtracking information.
    /// </summary>
    public static Either<R, string> ResultOrErrorString<T, R>(this Parser<T, R> p, InputStream<T> s) {
        var result = p(s);
        if (result.Result.Valid)
            return new(result.Result.Value);
        return new(s.ShowAllFailures(result.ErrorOrThrow));
    }
}
}