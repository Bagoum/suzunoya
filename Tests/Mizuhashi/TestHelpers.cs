using System;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using BagoumLib;
using Mizuhashi;
using NUnit.Framework;

namespace Tests.Mizuhashi {
public static class TestHelpers {
    public static void AssertSuccess<T, R>(this Parser<T, R> p, T[] s, R expect) => AssertSuccessAny(p, s, x => {
        Assert.AreEqual(expect, x);
        return true;
    });
    public static void AssertSuccess<R>(this Parser<char, R> p, string s, R expect) => AssertSuccessAny(p, s.Replace("\r\n", "\n").ToCharArray(), x => {
        Assert.AreEqual(expect, x);
        return true;
    });
    public static void AssertSuccessAny<T, R>(this Parser<T, R> p, T[] s, Func<R, bool> verify) {
        var res = p.Run(s, out var strm);
        if (res.Status != ResultStatus.OK)
            Assert.Fail(strm.ShowAllFailures(res.ErrorOrThrow));
        if (res.Error is { } err)
            Console.WriteLine("Success with errors:\n" + strm.ShowAllFailures(err));
        if (!strm.Empty)
            Console.WriteLine($"Success with incomplete parse:\n{strm.TokenWitness.ShowConsumed(0, strm.Index)}|" +
                              $"{strm.TokenWitness.ShowConsumed(strm.Index, strm.Source.Length)}");
        Assert.IsTrue(verify(res.Result.Value));
    }

    public static void AssertSuccessAny<R>(this Parser<char, R> p, string s, Func<R, bool> verify) =>
        AssertSuccessAny(p, s.ToCharArray(), verify);

    public static void AssertFail<T, R>(this Parser<T, R> p, T[] s, ParserError e, int? pos=null, params ParserError[] backtracks) {
        var result = p.Run(s, out var strm);
        if (result.Result.Valid)
            Assert.Fail("Expected the parser to fail, but it succeeded");
        var resultErr = result.ErrorOrThrow;
        if (pos.Try(out var position)) {
            var exp = new LocatedParserError(position, e);
            var errs = new LocatedParserError(resultErr.Index, resultErr.Error.Flatten());
            if (!Equals(exp, errs))
                Assert.Fail($"Expecting\n{exp.Show(strm)}\n~~~\n but instead received\n~~~\n{errs.Show(strm)}");
        } else if (!Equals(e, resultErr.Error)) 
                Assert.Fail($"Expecting\n{e.Show(strm, resultErr.Index, resultErr.End)}\n~~~but instead received~~~\n{resultErr.Error.Show(strm, resultErr.Index, resultErr.End)}\n" +
                            $"~~~as part of complete message~~~\n{strm.ShowAllFailures(result.ErrorOrThrow)}");
        try {
            AssertHelpers.ListEq(strm.Rollbacks.Select(u => u.Error).ToList(), backtracks);
        } catch (AssertionException ae) {
            throw new AssertionException(
                $"\nBacktracks did not match for error case:\n{strm.ShowAllFailures(result.ErrorOrThrow)}", ae);
        }
        Console.WriteLine($"\nSuccessfully tested error case:\n{strm.ShowAllFailures(result.ErrorOrThrow)}");
    }

    public static void AssertFail<R>(this Parser<char, R> p, string s, ParserError e, int? pos = null,
        params ParserError[] backtracks) => 
        p.AssertFail(s.ToCharArray(), e, pos, backtracks);
    
    public static void AssertFail<T, R>(this Parser<T, R> p, T[] s, string exp) {
        var res = p.Run(s, out var strm).Error?.Show(strm);
        if (res == null)
            Assert.Fail("Did not receive an error from execution.");
        if (res != exp) {
            var firstdiff = Enumerable.Range(0, Math.Min(res!.Length, exp.Length)).Where(i => res[i] != exp[i]).FirstOrNull();
            var firstdiff_msg = firstdiff.Try(out var fd) ?
                $" Text differs at index {fd} (expected {exp[fd]}, found {res[fd]})" :
                "";
            Assert.Fail($"Parser output is not as expected.{firstdiff_msg}\n" +
                        $"Expected (Length {exp.Length}):\n~~~\n{exp}\n~~~\n" +
                        $"But received (Length {res.Length}):\n~~~\n{res}");
        } else
            Console.WriteLine($"\nSuccessfully tested error case:\n{exp}");
    }

    public static void AssertFail<R>(this Parser<char, R> p, string s, string exp) =>
        p.AssertFail(s.ToCharArray(), exp);

    public static void AssertFailRegex<T, R>(this Parser<T, R> p, T[] s, string regex) {
        var res = p.Run(s, out var strm).Error?.Show(strm);
        if (res == null)
            Assert.Fail("Did not receive an error from execution.");
        if (!new Regex(regex).Match(res).Success)
            Assert.Fail($"Could not find pattern `{regex}` in:\n{res}");
        Console.WriteLine($"Successfully found failure pattern `{regex}` in:\n{res}");
    }

    public static void AssertFailRegex<R>(this Parser<char, R> p, string s, string regex) =>
        p.AssertFailRegex(s.ToCharArray(), regex);

    public static ParseResult<R> Run<T, R>(this Parser<T, R> p, T[] s, out InputStream<T> strm) => 
        p(strm = new(s, "test parser"));
}
}