using System.Collections.Generic;
using System.Reactive;
using BagoumLib.Functional;
using Mizuhashi;
using NUnit.Framework;
using static Mizuhashi.Combinators;
using static Tests.Mizuhashi.TestHelpers;
using static Mizuhashi.ParserError;

namespace Tests.Mizuhashi {

public class TestParsing1 {

    [Test]
    public void TestBasic() {
        var parseLower = Lower;
        parseLower.AssertSuccess("hello", 'h');
        parseLower.AssertFail("Hello", new Expected("lowercase character"), 0);
    }
    
    [Test]
    public void SepBy() {
        var parseIntList = ParseInt.SepBy(Char(','));
        var parseIntList1 = ParseInt.SepBy1(Char(','));
        parseIntList.AssertSuccess("abc", new List<int>());
        parseIntList.AssertSuccess("", new List<int>());
        parseIntList1.AssertFail("", new IncorrectNumber(1, 0, null, 
            new(0, new Expected("at least one digit"))), 0);
        parseIntList.AssertSuccess("2,366,41abc", new List<int>(){2,366,41});
        parseIntList.ThenEOF().AssertFail("abc", new OneOf(new() {
            new Expected("at least one digit"),
            new Expected("end of file")
        }), 0);
        parseIntList.ThenEOF().AssertFail("1,1a", new Expected("end of file"),3);
        parseIntList.AssertFail("1,1,a", new Expected("at least one digit"),4);
    }

    [Test]
    public void TestFunctional() {
        Char('a').ThenIg(Char('b')).AssertSuccess("ab", 'a');
        Char('a').IgThen(Char('b')).AssertSuccess("ab", 'b');
        Char('a').Then(Char('b')).AssertSuccess("ab", ('a', 'b'));
    }
    [Test]
    public void TestFunctionalErrs() {
        Char('a').ThenIg(Char('b')).AssertFail("ac", new ExpectedChar('b'));
    }
    
    [Test]
    public void TestEOF() {
        Lower.ThenEOF().AssertFail("hello", "Error at Line 1, Col 2:\n" +
                                                    "hello\n" +
                                                    "h| <- at this location\n" +
                                                    "Expected end of file");
    }

    [Test]
    public void TestSuccessError() {
        var oneOrTwo = Char('(')
            .IgThen(ParseInt.Then(Char(',').IgThen(Whitespace).IgThen(ParseInt).Opt()))
            .ThenIg(Char(')'));
        oneOrTwo.AssertSuccess("(1)", (1, Maybe<int>.None));
        oneOrTwo.AssertSuccess("(1, 6)", (1, 6));
        oneOrTwo.AssertFail("(1 6)", new OneOf(new() {
            new ExpectedChar(','),
            new ExpectedChar(')')
        }), 2);
    }

    private record Email(string name, string domain, string tld) {
        public override string ToString() => $"{name}@{domain}.{tld}";
    }

    private static readonly Parser<Email> parseEmail = Sequential(
        Many1Satisfy(char.IsLetterOrDigit, "letter or digit").Label("Email name"),
        Char('@'),
        Many1Satisfy(char.IsLetter, "letterA").Label("Email domain"),
        Char('.'),
        Many1Satisfy(char.IsLetter, "letterB").Label("Email TLD"),
        (name, _, domain, _, tld) => new Email(name, domain, tld));

    
    [Test]
    public void TestLabel() {
        parseEmail.AssertFail("!!", new Labelled("Email name", 
            new(new LocatedParserError(0, new Expected("at least one letter or digit")))));
        parseEmail.AssertFail("name!!", new ExpectedChar('@'), 4);
        parseEmail.AssertFail("name@!!", new Labelled("Email domain", 
            new(new LocatedParserError(5, new Expected("at least one letterA")))), 5);
        parseEmail.AssertFail("name@site!!", new ExpectedChar('.'), 9);
        parseEmail.AssertFail("name@site.!", new Labelled("Email TLD", 
            new(new LocatedParserError(10, new Expected("at least one letterB")))), 10);
        parseEmail.AssertSuccess("name@site.com", new Email("name", "site", "com"));
    }

    [Test]
    public void TestMany() {
        var parseEmails = parseEmail.ThenIg(Newline).Many();
        var parseEmails1 = parseEmail.ThenIg(Newline).Many1();
        var parse3Emails = parseEmail.ThenIg(Newline).Repeat(3);
        parseEmails.AssertSuccess("!!!", new List<Email>());
        parseEmails1.AssertFail("!!!", new IncorrectNumber(1, 0, null,
            new LocatedParserError(0, new Labelled("Email name", new(
                new LocatedParserError(0, new Expected("at least one letter or digit")))))));
        parseEmails.AssertFail("a@b.net\nb@c.com", new Expected("newline"), 15);
        parseEmails.AssertSuccess("a@b.net\nb@c.com\n", new() {new("a", "b", "net"), new("b", "c", "com")});
        parseEmails1.AssertSuccess("a@b.net\nb@c.com\n", new() {new("a", "b", "net"), new("b", "c", "com")});
        parse3Emails.AssertFail("a@b.net\nb@c.com\n", new IncorrectNumber(3, 2, null, 
            new LocatedParserError(16, new Labelled("Email name", new(
                new LocatedParserError(16, new Expected("at least one letter or digit")))))));
        parse3Emails.AssertSuccess("a@b.net\nb@c.com\nc@d.com\n", new() {
            new("a", "b", "net"), new("b", "c", "com"), new("c", "d", "com")
        });
    }

}
}