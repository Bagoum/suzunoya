using System;
using System.Linq;
using System.Reactive;
using BagoumLib;
using Mizuhashi;
using NUnit.Framework;

namespace Tests.Mizuhashi {
public static class TestHelpers {
    public static void AssertSuccess<R>(this Parser<R> p, string s, R expect) {
        var res = p.Run(s.Replace("\r\n", "\n"), out var strm);
        if (res.Status != ResultStatus.OK)
            Assert.Fail(strm.ShowAllFailures(res.ErrorOrThrow));
        Assert.AreEqual(expect, res.Result.Value);
    }

    public static void AssertFail<R>(this Parser<R> p, string s, ParserError e, int? pos=null, params ParserError[] backtracks) {
        var result = p.Run(s, out var strm);
        if (result.Result.Valid)
            Assert.Fail("Expected the parser to fail, but it succeeded");
        var resultErr = result.ErrorOrThrow;
        if (pos.Try(out var position)) {
            var exp = new LocatedParserError(position, e);
            var errs = new LocatedParserError(resultErr.Index, resultErr.Error.Flatten());
            if (!Equals(exp, errs))
                Assert.Fail($"Expecting\n{exp.Show(s)}\n~~~\n but instead received\n~~~\n{errs.Show(s)}");
        } else if (!Equals(e, resultErr.Error)) 
                Assert.Fail($"Expecting\n{e.Show(s)}\n~~~but instead received~~~\n{resultErr.Error.Show(s)}\n" +
                            $"~~~as part of complete message~~~\n{strm.ShowAllFailures(result.ErrorOrThrow)}");
        try {
            AssertHelpers.ListEq(strm.Rollbacks.SelectNotNull(u => u?.Error).ToList(), backtracks);
        } catch (AssertionException ae) {
            throw new AssertionException(
                $"\nBacktracks did not match for error case:\n{strm.ShowAllFailures(result.ErrorOrThrow)}", ae);
        }
        Console.WriteLine($"\nSuccessfully tested error case:\n{strm.ShowAllFailures(result.ErrorOrThrow)}");
    }
    public static void AssertFail<R>(this Parser<R> p, string s, string exp) {
        var res = p.Run(s, out _).Error?.Show(s);
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

    public static ParseResult<R> Run<R>(this Parser<R> p, string s, out InputStream strm) => 
        p(strm = new("test parser", s, default!));
}
}