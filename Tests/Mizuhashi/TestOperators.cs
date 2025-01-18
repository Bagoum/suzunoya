using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BagoumLib.Functional;
using Mizuhashi;
using NUnit.Framework;
using static Mizuhashi.Combinators;

namespace Tests.Mizuhashi {
public class TestOperators {
    private record Tree {
        public record Prefix(string Op, Tree Nested) : Tree {
            public override string ToString() => $"({Op}{Nested})";
        }

        public record Postfix(string Op, Tree Nested) : Tree {
            public override string ToString() => $"({Nested}{Op})";
        }

        public record Infix(string Op, Tree Left, Tree Right) : Tree {
            public override string ToString() => $"({Left}{Op}{Right})";
        }
        
        public record Char(char C) : Tree {
            public override string ToString() => $"{C}";
        }
    }

    //private static Parser<char> opStart = 
    //    Combinators.AnyOf(":!#$%&*+./<=>?@\\^|-~".ToCharArray());
    private static Parser<char, char> opTail = 
        Combinators.AnyOf(":!#$%&*+./<=>?@\\^|-~".ToCharArray());

    //Operator parser that doesn't allow operators to be next to each other without spaces in between.
    // This is not the only way to handle an operator parser; see TestLexing for an example that uses
    // a trie during tokenization to separate adjacent operators.
    private static Parser<char, string> reservedOpWithSpaces(string op) {
        var str = String(op);
        var isContinued = new ParserError.Unexpected($"more operator characters after '{op}'");
        return inp => {
            var ss0 = inp.Stative;
            var skip = 0;
            for (; skip < inp.Remaining; ++skip) 
                if (!char.IsWhiteSpace(inp.CharAt(skip)))
                    break;
            inp.Step(skip);
            var result = str(inp);
            if (!result.Result.Valid)
                return result;
            var ss1 = inp.Stative;
            var postOpChars = opTail(inp);
            inp.RollbackFast(ss1);
            if (postOpChars.Result.Valid) {
                inp.RollbackFast(ss0);
                return new(isContinued, ss0.Index);
            }
            for (skip = 0; skip < inp.Remaining; ++skip) 
                if (!char.IsWhiteSpace(inp.CharAt(skip)))
                    break;
            return new(result.Result, result.Error, ss0.Index, inp.Step(skip));
        };
    }

    private static Operator<char, Tree, string> prefix(string op, int priority) => 
        new Operator<char, Tree, string>.Prefix(
            reservedOpWithSpaces(op), (s, x) => new Tree.Prefix(s, x), priority);
    private static Operator<char, Tree, string> postfix(string op, int priority) => 
        new Operator<char, Tree, string>.Postfix(
            reservedOpWithSpaces(op), (x, s) => new Tree.Postfix(s, x), priority);
    private static Operator<char, Tree, string> infix(string op, int priority, Associativity assoc) => 
        new Operator<char, Tree, string>.Infix(
            reservedOpWithSpaces(op),
            (x, s, y) => new Tree.Infix(s, x, y),
            assoc,
            priority);
    
    private static FPrefix<char, Tree> fprefix(char op) => 
        new(op, (s, x) => new Tree.Prefix(op.ToString(), x), c => null);
    private static FPostfix<char, Tree> fpostfix(char op) => 
        new(op, (x, s) => new Tree.Postfix(op.ToString(), x), c => null);
    private static FInfix<char, Tree> finfix(char op, int priority, Associativity assoc) => 
        new(op, (x, s, y) => new Tree.Infix(op.ToString(), x, y), assoc, priority, c => null);
    
    private static readonly Operator<char, Tree, string>[] operators = {
        prefix("-", 10), postfix("++", 10),
        infix("+++", 9, Associativity.Right),
        infix("*", 8, Associativity.Left), infix("#", 8, Associativity.None),
        infix("+", 6, Associativity.Left), infix("~", 6, Associativity.Right)
    };

    private static readonly Parser<char, Tree> term = AsciiLetter.FMap(Tree (a) => new Tree.Char(a));
    //legacy operator parser
    private static readonly Parser<char, Tree> opParse1 = 
        ParseOperators(operators, term);

    //new operator parser with single-token lookup table
    private static readonly Parser<char, Tree> opParse2 =
        ParseOperatorsFast(
            ParsePrefixPostfixFast(term, EqualityComparer<char>.Default, [fprefix('-'), fprefix('!')], [fpostfix('.'),fpostfix('!')]), 
            EqualityComparer<char>.Default, [
            finfix('^', 9, Associativity.Right),
            finfix('*', 8, Associativity.Left), finfix('#', 8, Associativity.None),
            finfix('+', 6, Associativity.Left), finfix('~', 6, Associativity.Right)
        ]);

    [Test]
    public void TestAssocV2() {
        opParse2.AssertSuccessAny("-x!+!y", x => AssertHelpers.AssertStringEq("(((-x)!)+(!y))", x));
        opParse2.AssertFailRegex("-x+!", "Expected ASCII");
        opParse2.AssertFailRegex("-x+.y", "Expected ASCII");
        opParse2.AssertSuccessAny("x+y", x => AssertHelpers.AssertStringEq("(x+y)", x));
        opParse2.AssertSuccessAny("x*y*z*a", x => x.ToString() == "(((x*y)*z)*a)");
        opParse2.AssertSuccessAny("x*y+z*a", x => x.ToString() == "((x*y)+(z*a))");
        opParse2.AssertFailRegex("x*y#z", @"Found ambiguous non-associative operator # when parsing the left-associative operator \*");
        opParse2.AssertFailRegex("x#y#z", "multiple non-associative operators of the same priority");
        opParse2.AssertSuccessAny("x#y", x => x.ToString() == "(x#y)");
        opParse2.AssertFailRegex("x+y~z", @"Found ambiguous right-associative operator ~ when parsing the left-associative operator \+");
        opParse2.AssertFailRegex("x~y+z", @"Found ambiguous left-associative operator \+ when parsing the right-associative operator ~");
        opParse2.AssertSuccessAny("x~y~z~a", x => x.ToString() == "(x~(y~(z~a)))");
        opParse2.AssertSuccessAny("--x!!~!!y!!.!^c", x => AssertHelpers.AssertStringEq(
            "((((-(-x))!)!)~((((((!(!y))!)!).)!)^c))", x));
        //test case where term fails after operator
    }

    [Test]
    public void TestAssoc() {
        //++* fails under reservedOp
        opParse1.AssertSuccessAny("- x++*y", x => AssertHelpers.AssertStringEq("(-x)", x));
        opParse1.AssertSuccessAny("-x++ *y", x => AssertHelpers.AssertStringEq("(((-x)++)*y)", x));
        opParse1.AssertSuccessAny("x*y*z*a", x => x.ToString() == "(((x*y)*z)*a)");
        opParse1.AssertSuccessAny("x*y+z*a", x => x.ToString() == "((x*y)+(z*a))");
        opParse1.AssertFailRegex("x*y#z", @"Found ambiguous non-associative operator # when parsing the left-associative operator \*");
        opParse1.AssertFailRegex("x#y#z", "multiple non-associative operators of the same priority");
        opParse1.AssertSuccessAny("x#y", x => x.ToString() == "(x#y)");
        opParse1.AssertFailRegex("x+y~z", @"Found ambiguous right-associative operator ~ when parsing the left-associative operator \+");
        opParse1.AssertFailRegex("x~y+z", @"Found ambiguous left-associative operator \+ when parsing the right-associative operator ~");
        opParse1.AssertSuccessAny("x~y~z~a", x => x.ToString() == "(x~(y~(z~a)))");
        opParse1.AssertSuccessAny("x+++y~z*a++ +++ -b*c++", x => AssertHelpers.AssertStringEq(
            "((x+++y)~((z*((a++)+++(-b)))*(c++)))", x));
        
    }

    [Test]
    public void TestReservedOp() {
        //Don't parse as ((x++)+y)
        opParse1.AssertSuccessAny("x+++y", x => AssertHelpers.AssertStringEq("(x+++y)", x));
        opParse1.AssertSuccessAny("x++ +y", x => AssertHelpers.AssertStringEq("((x++)+y)", x));
    }
    
}
}