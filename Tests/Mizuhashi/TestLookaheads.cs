using System.Collections.Generic;
using System.Reactive;
using BagoumLib.Functional;
using Mizuhashi;
using NUnit.Framework;
using static Mizuhashi.Combinators;
using static Tests.Mizuhashi.TestHelpers;
using static Mizuhashi.ParserError;

namespace Tests.Mizuhashi {

public class TestLookaheads {
    [Test]
    public void TestTry() {
        //By using char instead of string parsers we de-atomize them, allowing fatal failures in the middle
        var pBrack = Char('[').IgThen(Char('b')).ThenIg(Char(']'));
        var pAC = Char('a').Then(Char('c'));
        var p1 = Char('a').ThenTry(pBrack).Or(pAC);
        var p2 = Char('a').Then(pBrack).Or(pAC);
        
        p1.AssertSuccess("a[b]", ('a', 'b'));
        p2.AssertSuccess("a[b]", ('a', 'b'));
        
        p1.AssertSuccess("ac", ('a', 'c'));
        p2.AssertFail("ac", new ExpectedChar('[')); //Doesn't backtrack since 'a' was consumed by first branch
        
        p1.AssertFail("a[f]", new ExpectedChar('b'));
        p2.AssertFail("a[f]", new ExpectedChar('b'));
        
        p1.AssertFail("ax", new ExpectedChar('c'), null, new ExpectedChar('['));
        p2.AssertFail("ax", new ExpectedChar('['));
    }
    
    [Test]
    public void TestFollowedBy() {
        var parserA = Char('0').IsNotPresent("leading zero").IgThen(ParseInt);
        var parserB = Satisfy(c => c != '0').IsPresent().IgThen(ParseInt);
        var parserC = parserB.Label("int without leading zero");

        parserA.AssertSuccess("123", 123);
        parserB.AssertSuccess("123", 123);
        parserC.AssertSuccess("123", 123);
        parserA.AssertFail("red", new Expected("at least one digit"));
        parserB.AssertFail("red", new Expected("at least one digit"));
        parserC.AssertFail("red", new Labelled("int without leading zero", new Expected("at least one digit")));
        parserA.AssertFail("0123", new Unexpected("leading zero"));
        parserB.AssertFail("0123", new UnexpectedChar('0'));
        parserC.AssertFail("0123", new Labelled("int without leading zero", new UnexpectedChar('0')));
    }
    

}
}