using System.Collections.Generic;
using System.Reactive;
using Mizuhashi;
using NUnit.Framework;
using static Mizuhashi.Combinators;
using static Tests.Mizuhashi.TestHelpers;

namespace Tests.Mizuhashi {

public class TestParsing1 {

    [Test]
    public void TestBasic() {
        var parseLower = Lower<Unit>();
        parseLower.AssertSuccess("hello", 'h');
        parseLower.AssertFail("Hello", new ParserError.Expected("lowercase character"), new(0, 1, 0));

    }
    
    [Test]
    public void SepBy() {
        var parseIntList = ParseInt<Unit>().SepBy(Char<Unit>(','));
        var parseIntList1 = ParseInt<Unit>().SepBy1(Char<Unit>(','));
        parseIntList.AssertSuccess("abc", new List<int>());
        parseIntList.AssertSuccess("", new List<int>());
        parseIntList1.AssertFail("", new ParserError.Expected("at least one digit"), new Position(0, 1, 0));
        parseIntList.AssertSuccess("2,366,41abc", new List<int>(){2,366,41});
        parseIntList.ThenEOF().AssertFail("abc", new ParserError.OneOf(new() {
            new ParserError.Expected("at least one digit"),
            new ParserError.Expected("end of file")
        }), new Position(0, 1, 0));
        parseIntList.ThenEOF().AssertFail("1,1a", new ParserError.Expected("end of file"),new Position(3, 1, 0));
        parseIntList.AssertFail("1,1,a", new ParserError.Expected("at least one digit"),new Position(4, 1, 0));
    }

    [Test]
    public void TestFunctional() {
        Char<Unit>('a').ThenIg(Char<Unit>('b')).AssertSuccess("ab", 'a');
        Char<Unit>('a').IgThen(Char<Unit>('b')).AssertSuccess("ab", 'b');
        Char<Unit>('a').Then(Char<Unit>('b')).AssertSuccess("ab", ('a', 'b'));
    }
    
    [Test]
    public void TestEOF() {
        Lower<Unit>().ThenEOF().AssertFail("hello", "Error at Line 1, Col 2:\n" +
                                                    "hello\n" +
                                                    ".^\n" +
                                                    "Expected end of file");
    }


}
}