using System.Collections.Generic;
using System.Reactive;

namespace Mizuhashi {
public static partial class Combinators {
    

    /// <summary>
    /// Applies a parser as many times as possible up to maxTimes, and returns the list of all results.
    /// </summary>
    /// <param name="p"></param>
    /// <param name="minTimes">Minimum number of times to apply the parser (inclusive). Will error if fewer results are found.</param>
    /// <param name="maxTimes">Maximum number of times to apply the parser (inclusive).</param>
    public static Parser<T, List<R>> Repeat<T, R>(this Parser<T, R> p, int minTimes, int maxTimes) {
        if (maxTimes == 0) return PReturn<T, List<R>>(new List<R>());
        return input => {
            var results = new List<R>();
            ParseResult<R> next = default;
            var start = input.Index;
            for (int ii = 0; ii < maxTimes; ++ii) {
                next = p(input);
                if (next.Status == ResultStatus.FATAL)
                    return next.CastFailure<List<R>>();
                else if (next.Status == ResultStatus.ERROR)
                    if (results.Count >= minTimes)
                        goto finalize;
                    else
                        return new((results.Count == 0 && next.Error != null) ? 
                            next.Error?.Error! :
                            new ParserError.IncorrectNumber(minTimes, results.Count, null, next.Error), next.Start);
                else if (!next.Consumed)
                    return new(
                        new ParserError.Failure("`Many` parser parsed an object without consuming text."), next.Start);
                else
                    results.Add(next.Result.Value);
            }
            finalize: ;
            return new(results, next.Error, start, next.End);
        };
    }

    /// <summary>
    /// Applies a parser repeatedly until it errors, and returns a list of all results.
    /// <br/>Note that if the parser errors fatally, this will error fatally as well.
    /// <br/>To avoid infinite recursion, if the parser succeeds without consumption, this will fail.
    /// <br/>FParsec many
    /// </summary>
    /// <param name="p">Parser to apply repeatedly</param>
    /// <param name="atleastOne">If true, then will require at least one result</param>
    /// <returns></returns>
    public static Parser<T, List<R>> Many<T, R>(this Parser<T, R> p, bool atleastOne=false) => input => {
        var results = new List<R>();
        var start = input.Index;
        while (true) {
            var next = p(input);
            if (next.Status == ResultStatus.FATAL)
                return next.CastFailure<List<R>>();
            else if (next.Status == ResultStatus.ERROR)
                return (results.Count == 0 && atleastOne) ?
                    new(next.Error?.Error ?? new ParserError.IncorrectNumber(atleastOne ? 1 : 0, results.Count, 
                        null, next.Error), start) :
                    new(results, next.Error, start, next.End);
            else if (!next.Consumed)
                return new(
                    new ParserError.Failure("`Many` parser parsed an object without consuming text."), next.Start);
            else
                results.Add(next.Result.Value);
        }
    };

    /// <summary>
    /// Applies a parser repeatedly until it errors, and returns a list of all results.
    /// <br/>Requires at least one value to be parsed.
    /// <br/>See <see cref="Many{T,R}"/>.
    /// </summary>
    public static Parser<T, List<R>> Many1<T, R>(this Parser<T, R> p) => Many(p, true);
    
    
    /// <summary>
    /// Applies a parser repeatedly until it errors.
    /// <br/>Note that if the parser errors fatally, this will error fatally as well.
    /// <br/>To avoid infinite recursion, if the parser succeeds without consumption, this will fail.
    /// <br/>FParsec many
    /// </summary>
    /// <param name="p">Parser to apply repeatedly</param>
    /// <param name="atleastOne">If true, then will require at least one result</param>
    /// <returns></returns>
    public static Parser<T, Unit> SkipMany<T, R>(this Parser<T, R> p, bool atleastOne=false) => input => {
        bool foundOne = false;
        var start = input.Index;
        while (true) {
            var next = p(input);
            if (next.Status == ResultStatus.FATAL)
                return next.CastFailure<Unit>();
            else if (next.Status == ResultStatus.ERROR)
                return (!foundOne && atleastOne) ?
                    new(new ParserError.IncorrectNumber(atleastOne ? 1 : 0, 0, null, next.Error), start) :
                    new(Unit.Default, next.Error, start, next.End);
            else if (!next.Consumed)
                return new(
                    new ParserError.Failure("`SkipMany` parser parsed an object without consuming text."), next.Start);
            else
                foundOne = true;
        }
    };

    /// <summary>
    /// Applies a parser repeatedly until it errors.
    /// <br/>Requires at least one value to be parsed.
    /// <br/>See <see cref="SkipMany{T,R}"/>.
    /// </summary>
    public static Parser<T, Unit> SkipMany1<T, R>(this Parser<T, R> p) => SkipMany(p, true);


    /// <summary>
    /// Parse `p (sep p)*`. If atleastOne is false, then allows parsing nothing. `sep` may be non-consuming.
    /// <br/>FParsec sepBy
    /// </summary>
    public static Parser<T, List<R>> SepBy<T, R, U>(this Parser<T, R> ele, Parser<T, U> sep, bool atleastOne = false) =>
        input => {
            var results = new List<R>();
            var start = input.Index;
            var next = ele(input);
            if (next.Status == ResultStatus.FATAL)
                return next.CastFailure<List<R>>();
            else if (next.Status == ResultStatus.ERROR)
                return atleastOne ?
                    new(next.Error?.Error ?? 
                        new ParserError.IncorrectNumber(atleastOne ? 1 : 0, results.Count, null, next.Error), 
                        start, next.End) :
                    new(results, next.Error, start, next.End);
            else
                results.Add(next.Result.Value);

            while (true) {
                var sepParsed = sep(input);
                if (sepParsed.Status == ResultStatus.FATAL)
                    return sepParsed.CastFailure<List<R>>();
                else if (sepParsed.Status == ResultStatus.ERROR)
                    return new(results, next.MergeErrors(sepParsed), start, next.End);

                next = ele(input);
                if (next.Status == ResultStatus.OK)
                    results.Add(next.Result.Value);
                else if (next.Status == ResultStatus.ERROR && !sepParsed.Consumed)
                    return new(results, sepParsed.MergeErrors(next), start, next.End);
                else
                    return next.CastFailure<List<R>>();
            }
        };

    /// <summary>
    /// Parse `p (sep p)*`.
    /// </summary>
    public static Parser<T, List<R>> SepBy1<T, R, U>(this Parser<T, R> ele, Parser<T, U> sep) => SepBy(ele, sep, true);
    
    

    /// <summary>
    /// Parse `p (sep p)*`. Both p and sep are included in the results list.
    /// </summary>
    public static Parser<T, List<R>> SepByAll<T, R>(this Parser<T, R> ele, Parser<T, R> sep, int minPs = 0) {
        var empty = new List<R>();
        return input => {
            List<R>? results = null;
            var start = input.Index;
            var next = ele(input);
            int ps = 0;
            ParseResult<List<R>> ReturnOnError() =>
                minPs > ps ?
                    new((ps == 0 && next.Error != null) ? next.Error?.Error! : 
                        new ParserError.IncorrectNumber(minPs, ps, null, next.Error), 
                        start, next.End) :
                    new(results ??= empty, next.Error, start, next.End);

            if (next.Status == ResultStatus.FATAL)
                return next.CastFailure<List<R>>();
            else if (next.Status == ResultStatus.ERROR)
                return ReturnOnError();
            else
                (results = new()).Add(next.Result.Value);

            for (ps = 1;; ++ps) {
                var sepParsed = sep(input);
                if (sepParsed.Status == ResultStatus.FATAL)
                    return new(sepParsed.Error, start, sepParsed.End);
                else if (sepParsed.Status == ResultStatus.ERROR)
                    return ReturnOnError();
                else
                    results.Add(sepParsed.Result.Value);
                next = ele(input);
                if (next.Status == ResultStatus.OK)
                    results.Add(next.Result.Value);
                else
                    return new(next.Error, start, next.End);
            }
        };
    }
}
}