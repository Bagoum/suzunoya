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
    
    private static Parser<char, string> reservedOp(string op) {
        var str = String(op);
        var isContinued = new ParserError.Unexpected($"more operator characters after '{op}'");
        return inp => {
            var ss0 = inp.Stative;
            var result = str(inp);
            if (!result.Result.Valid)
                return result;
            var ss1 = inp.Stative;
            var postOpChars = opTail(inp);
            inp.RollbackFast(ss1);
            if (postOpChars.Result.Valid) {
                inp.RollbackFast(ss0);
                return new(isContinued, result.Start);
            }
            return result;
        };
    }

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
    
    private static readonly Operator<char, Tree, string>[] operators = {
        prefix("-", 10), postfix("++", 10),
        infix("+++", 9, Associativity.Right),
        infix("*", 8, Associativity.Left), infix("#", 8, Associativity.None),
        infix("+", 6, Associativity.Left), infix("~", 6, Associativity.Right)
    };

    private static readonly Parser<char, Tree> parser = 
        ParseOperators(operators, AsciiLetter.FMap(a => new Tree.Char(a) as Tree));

    [Test]
    public void TestAssoc() {
        //++* fails under reservedOp
        parser.AssertSuccessAny("- x++*y", x => AssertHelpers.AssertStringEq("(-x)", x));
        parser.AssertSuccessAny("-x++ *y", x => AssertHelpers.AssertStringEq("(((-x)++)*y)", x));
        parser.AssertSuccessAny("x*y*z*a", x => x.ToString() == "(((x*y)*z)*a)");
        parser.AssertSuccessAny("x*y+z*a", x => x.ToString() == "((x*y)+(z*a))");
        parser.AssertFailRegex("x*y#z", @"Found ambiguous non-associative operator # when parsing the left-associative operator \*");
        parser.AssertFailRegex("x#y#z", "multiple non-associative operators of the same priority");
        parser.AssertSuccessAny("x#y", x => x.ToString() == "(x#y)");
        parser.AssertFailRegex("x+y~z", @"Found ambiguous right-associative operator ~ when parsing the left-associative operator \+");
        parser.AssertFailRegex("x~y+z", @"Found ambiguous left-associative operator \+ when parsing the right-associative operator ~");
        parser.AssertSuccessAny("x~y~z~a", x => x.ToString() == "(x~(y~(z~a)))");
        parser.AssertSuccessAny("x+++y~z*a++ +++ -b*c++", x => AssertHelpers.AssertStringEq(
            "((x+++y)~((z*((a++)+++(-b)))*(c++)))", x));
    }

    [Test]
    public void TestReservedOp() {
        //Don't parse as ((x++)+y)
        parser.AssertSuccessAny("x+++y", x => AssertHelpers.AssertStringEq("(x+++y)", x));
        parser.AssertSuccessAny("x++ +y", x => AssertHelpers.AssertStringEq("((x++)+y)", x));
    }
    
}
}