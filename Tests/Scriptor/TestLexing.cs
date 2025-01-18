using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using JetBrains.Annotations;
using Mizuhashi;
using Mizuhashi.Lexers;
using NUnit.Framework;
using Scriptor.Definition;
using static Scriptor.Definition.Lexer;

namespace Tests.TScriptor {
public class TestLexing {
    [Test]
    public void TestLexing01() {
        var lexer = new RegexLexer<string>(
            new(@"((\s)+)", (p, s) => "space"),
            new(@"([a-z])([a-z]*)", (p, s) => $"ident:{s.Value}"),
            new(@"[+*]+", (p, s) => $"op:{s.Value}")
        );
        AssertHelpers.ListEq(lexer.Tokenize("hello + world"), new[] { "ident:hello", "space", "op:+", "space", "ident:world"});
    }
    
    [Test]
    public void TestExampleLanguage() {
        AssertTokenize("12.5f--+ gorilla'++", MakeFromSequence(
            (TokenType.Number, "12.5f"),
            (TokenType.Operator, "--"),
            (TokenType.Operator, "+"),
            (TokenType.InlineWhitespace, " "),
            (TokenType.Identifier, "gorilla'"),
            (TokenType.Operator, "++")
        ));
        
        AssertTokenize("hello12", MakeFromSequence((TokenType.Identifier, "hello12")));
        
        AssertTokenize("hello 12", MakeFromSequence(
            (TokenType.Identifier, "hello"), (TokenType.InlineWhitespace, " "), (TokenType.Number, "12")));
        
        AssertTokenize("12hello", MakeFromSequence((TokenType.Number, "12h"), (TokenType.Identifier, "ello")),
            "Number.*Identifier.*must be separated by whitespace or an operator");

        AssertTokenize(@"fn("")))""), )", MakeFromSequence(
            (TokenType.Identifier, "fn"), (TokenType.OpenParen, "("), (TokenType.String, "\")))\""),
            (TokenType.CloseParen, ")"), (TokenType.Comma, ","), (TokenType.InlineWhitespace, " "), 
            (TokenType.CloseParen, ")")
        ), "closing parenthesis is not matched");
        int k = 5;
    }

    private static void AssertTokenize(string source, Token[] result, string? postprocessErr = null) {
        var tokens = Lexer.Tokenize(ref source);
        AssertHelpers.ListEq(tokens, result);
        AssertHelpers.ThrowsMessage(postprocessErr, () => Lexer.Postprocess(source, tokens, out _, out _));
    }

    private static Token[] MakeFromSequence(params (TokenType type, string token)[] fragments) {
        var result = new Token[fragments.Length];
        var pos = new Position("", 0);
        for (int ii = 0; ii < fragments.Length; ++ii) {
            var (type, token) = fragments[ii];
            var tokenVal = type is TokenType.String ? token[1..^1] : token;
            var nPos = pos.Step(token, token.Length);
            result[ii] = new(type, new PositionRange(pos, nPos), tokenVal);
            pos = nPos;
        }
        return result;
    }

    [Test]
    public void TestRegexSpeed() {
        var keys = new[] { "red", "blue", "green", "orange", "purple", "black", "yellow", "brown", "pink", "white", "brown", "cat", "dog", "ferret", "raccoon", "bird"  };
        var regexes = keys.Select(x => new Regex(x, RegexOptions.Compiled)).ToArray();
        var groupNames = new string[keys.Length];
        var unified = new Regex(string.Join("|", keys.Select((x, i) => $"(?<{groupNames[i] = $"regexLexerGroup{i}"}>{x})")), RegexOptions.Compiled);

        var test_strings = new[] { "blargh", "greenery", "orangish", "purport " };

        void Compare(int itrs) {
            Console.WriteLine($"\nFor count {itrs}:");
            var t = new Stopwatch();
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr) {
                var str = test_strings[itr % test_strings.Length];
                for (int ir = 0; ir < regexes.Length; ++ir) {
                    var m = regexes[ir].Match(str);
                    if (m.Success)
                        break;
                }
            }
            t.Stop();
            Console.WriteLine($"Regex[]: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
            t.Restart();
            for (int itr = 0; itr < itrs; ++itr) {
                var str = test_strings[itr % test_strings.Length];
                var m = unified.Match(str);
                for (int ig = 0; ig < groupNames.Length; ++ig) {
                    if (m.Groups[groupNames[ig]].Success)
                        break;
                }
            }
            t.Stop();
            Console.WriteLine($"Unified: {t.ElapsedTicks} {t.ElapsedMilliseconds}");
        }

        Compare(1000000);
    }
    
}
}