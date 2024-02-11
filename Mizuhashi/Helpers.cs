using BagoumLib.Functional;
using JetBrains.Annotations;

namespace Mizuhashi {
/// <summary>
/// Helpers for calling parsers.
/// </summary>
[PublicAPI]
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
    /// Returns either the result of a parser, or its final failure, for a basic string parser.
    /// <br/>Does not include backtracking information, but that is stored in s.
    /// </summary>
    public static Either<R, LocatedParserError> ResultOrError<R>(this Parser<char, R> p, string s) {
        var result = p(new(s, "String parser"));
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