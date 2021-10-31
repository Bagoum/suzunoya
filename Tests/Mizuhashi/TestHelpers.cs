using System;
using System.Linq;
using System.Reactive;
using BagoumLib;
using Mizuhashi;
using NUnit.Framework;

namespace Tests.Mizuhashi {
public static class TestHelpers {
    public static void AssertSuccess<R>(this Parser<R> p, string s, R expect) {
        var res = p.Run(s);
        if (res.Status != ResultStatus.OK)
            Assert.Fail(res.Error?.Show(s));
        Assert.AreEqual(expect, res.Result.Value);
    }

    public static void AssertFail<R>(this Parser<R> p, string s, ParserError e, int? pos=null) {
        if (pos.Try(out var position)) {
            var exp = new LocatedParserError(position, e);
            var merrs = p.Run(s).Error;
            if (!merrs.Try(out var errs))
                Assert.Fail("Did not receive an error from execution.");
            errs = new LocatedParserError(errs.Index, errs.Error.Flatten());
            if (!Equals(exp, errs))
                Assert.Fail($"Expecting\n{exp.Show(s)}\n~~~\n but instead received\n~~~\n{errs.Show(s)}");
            else
                Console.WriteLine($"\nSuccessfully tested error case:\n{exp.Show(s)}");
        } else {
            var resultErr = p.Run(s).Error;
            if (!Equals(e, resultErr?.Error)) {
                Assert.Fail($"Expecting\n{e.Show(s)}\n~~~\n but instead received\n~~~\n{resultErr?.Error.Show(s)}");
            } else
                Console.WriteLine($"\nSuccessfully tested error case:\n{resultErr?.Show(s)}");
        }
    }
    public static void AssertFail<R>(this Parser<R> p, string s, string exp) {
        var res = p.Run(s).Error?.Show(s);
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

    public static ParseResult<R> Run<R>(this Parser<R> p, string s) => 
        p(new("test parser", s, default!));
}
}