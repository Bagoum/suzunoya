using System.Collections.Generic;
using System.Reactive;

namespace Mizuhashi {
public static partial class Combinators {
    

    /// <summary>
    /// Applies a parser a specific number of times, and returns the list of all results.
    /// </summary>
    public static Parser<List<R>> Repeat<R>(this Parser<R> p, int times) {
        if (times == 0) return PReturn(new List<R>());
        return input => {
            var results = new List<R>();
            ParseResult<R> next = default;
            var start = input.Index;
            for (int ii = 0; ii < times; ++ii) {
                next = p(input);
                if (next.Status == ResultStatus.FATAL)
                    return next.CastFailure<List<R>>();
                else if (next.Status == ResultStatus.ERROR)
                    return new(new ParserError.IncorrectNumber(times, results.Count, 
                        null, next.Error), next.Start);
                else if (!next.Consumed)
                    return new(
                        new ParserError.Failure("Many parser parsed an object without consuming text."), next.Start);
                else
                    results.Add(next.Result.Value);
            }
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
    public static Parser<List<R>> Many<R>(this Parser<R> p, bool atleastOne=false) => input => {
        var results = new List<R>();
        var start = input.Index;
        while (true) {
            var next = p(input);
            if (next.Status == ResultStatus.FATAL)
                return next.CastFailure<List<R>>();
            else if (next.Status == ResultStatus.ERROR)
                return (results.Count == 0 && atleastOne) ?
                    new(new ParserError.IncorrectNumber(atleastOne ? 1 : 0, results.Count, 
                        null, next.Error), start) :
                    new(results, next.Error, start, next.End);
            else if (!next.Consumed)
                return new(
                    new ParserError.Failure("Many parser parsed an object without consuming text."), next.Start);
            else
                results.Add(next.Result.Value);
        }
    };

    public static Parser<List<R>> Many1<R>(this Parser<R> p) => Many(p, true);
    
    
    /// <summary>
    /// Applies a parser repeatedly until it errors.
    /// <br/>Note that if the parser errors fatally, this will error fatally as well.
    /// <br/>To avoid infinite recursion, if the parser succeeds without consumption, this will fail.
    /// <br/>FParsec many
    /// </summary>
    /// <param name="p">Parser to apply repeatedly</param>
    /// <param name="atleastOne">If true, then will require at least one result</param>
    /// <returns></returns>
    public static Parser<Unit> SkipMany<R>(this Parser<R> p, bool atleastOne=false) => input => {
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
                    new ParserError.Failure("SkipMany parser parsed an object without consuming text."), next.Start);
            else
                foundOne = true;
        }
    };

    public static Parser<Unit> SkipMany1<R>(this Parser<R> p) => SkipMany(p, true);


    /// <summary>
    /// Parse `p (sep p)*`. If atleastOne is false, then allows parsing nothing.
    /// <br/>FParsec sepBy
    /// </summary>
    public static Parser<List<R>> SepBy<R, U>(this Parser<R> ele, Parser<U> sep, bool atleastOne = false) =>
        input => {
            var results = new List<R>();
            var start = input.Index;
            var next = ele(input);
            if (next.Status == ResultStatus.FATAL)
                return next.CastFailure<List<R>>();
            else if (next.Status == ResultStatus.ERROR)
                return atleastOne ?
                    new(new ParserError.IncorrectNumber(atleastOne ? 1 : 0, results.Count, 
                        null, next.Error), start) :
                    new(results, next.Error, start, next.End);
            else
                results.Add(next.Result.Value);

            while (true) {
                var sepParsed = sep(input);
                if (sepParsed.Status == ResultStatus.FATAL)
                    return sepParsed.CastFailure<List<R>>();
                else if (sepParsed.Status == ResultStatus.ERROR)
                    return new(results, next.Error, start, next.End);

                next = ele(input);
                if (next.Status == ResultStatus.OK)
                    results.Add(next.Result.Value);
                else
                    return next.CastFailure<List<R>>();
            }
        };

    public static Parser<List<R>> SepBy1<R, U>(this Parser<R> ele, Parser<U> sep) => SepBy(ele, sep, true);
    
    

    /// <summary>
    /// Parse `p (sep p)*`. Both p and sep are included in the results list.
    /// </summary>
    public static Parser<List<R>> SepByAll<R>(this Parser<R> ele, Parser<R> sep, int minPs = 0) {
        var empty = new List<R>();
        return input => {
            List<R>? results = null;
            var start = input.Index;
            var next = ele(input);
            int ps = 0;
            ParseResult<List<R>> ReturnOnError() =>
                minPs > ps ?
                    new(new ParserError.IncorrectNumber(minPs, ps, null, next.Error), start, next.End) :
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