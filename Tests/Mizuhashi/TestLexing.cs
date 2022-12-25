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

namespace Tests.Mizuhashi {
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

    private enum TokenType {
        /// <summary>
        /// Non-newline whitespaces.
        /// </summary>
        InlineWhitespace,
        /// <summary>
        /// Newline whitespace (only \n).
        /// </summary>
        Newline,
        /// <summary>
        /// Comment (either /* block comment */ or  // one-line comment).
        /// </summary>
        Comment,
        /// <summary>
        /// A reserved keyword (that would otherwise be parsed as an identifier).
        /// </summary>
        Keyword,
        /// <summary>
        /// A special operator, such as :: or ->, which has functionality more
        ///  advanced than calling a function.
        /// </summary>
        SpecialOperator,
        /// <summary>
        /// A operator that calls a function, such as + or *.
        /// </summary>
        Operator,
        //special symbols
        // We don't *need* to make these separate from operator, but it's convenient for parsing
        /// <summary>
        /// Open parentheses, (
        /// </summary>
        OpenParen,
        /// <summary>
        /// Close parentheses, )
        /// </summary>
        CloseParen,
        /// <summary>
        /// Open bracket, [
        /// </summary>
        OpenBracket,
        /// <summary>
        /// Close bracket, ]
        /// </summary>
        CloseBracket,
        /// <summary>
        /// Open brace, {
        /// </summary>
        OpenBrace,
        /// <summary>
        /// Close brace, }
        /// </summary>
        CloseBrace,
        Comma,
        Semicolon,
        /// <summary>
        /// An identifier for a variable, method, class, type, etc.
        /// </summary>
        Identifier,
        /// <summary>
        /// A value, such as a number or string, with a special format.
        /// </summary>
        SpecialValue,
        /// <summary>
        /// A number (integer or float). Can be preceded by signs and postceded by numerical multipliers pi, p, h, c, s.
        /// </summary>
        Number,
        /// <summary>
        /// Strings, bounded by " ".
        /// </summary>
        String,
        /// <summary>
        /// A non-token that has been inserted by a postprocessor. These can be filtered out.
        /// </summary>
        Noop
    }

    private readonly struct Token {
        public string Content { get; }
        
        public PositionRange Position { get; }
        
        public TokenType Type { get; }
        private (TokenType, string, PositionRange) Tuple => (Type, Content, Position);

        public Token(TokenType type, Position p, Match m) {
            Type = type;
            Content = m.Value;
            Position = p.CreateRange(m.Value, m.Length);
        }
        
        public Token(TokenType type, Position p, string content) {
            Type = type;
            Content = content;
            Position = p.CreateRange(content, content.Length);
        }

        public override string ToString() => string.IsNullOrWhiteSpace(Content) ? $"({Type})" : $"\"{Content}\" ({Type})";

        public override bool Equals(object? obj) => obj is Token t && this == t;
        public override int GetHashCode() => Tuple.GetHashCode();
        public static bool operator==(Token x, Token y) => x.Tuple == y.Tuple;
        public static bool operator !=(Token x, Token y) => !(x == y);
        
    }
    private static RegexTokenizer<Token> T([RegexPattern] string pattern, TokenType t) =>
        new(pattern, (p, m) => (new Token(t, p, m), m.Value.Length));
    private static RegexTokenizer<Token> T([RegexPattern] string pattern, Func<Position, Match, Token> t) =>
        new(pattern, (p, m) => (t(p, m), m.Value.Length));
    
    private static RegexTokenizer<Token> T([RegexPattern] string pattern, Func<Position, Match, Maybe<(Token, int)>>t) =>
        new(pattern, t);
    
    [Test]
    public void TestExampleLanguage() {
        var uLetter = @"\p{L}";
        var num = @"[0-9]";
        var numMult = @"pi?|[hfsc]";
        var specialOps = new[] { "::", "->" }.ToHashSet();
        var keywords = new[] { "function", "let", "true", "false" }.ToHashSet();
        var operators = new[] {
            "&&", "||", "==", "!=", "<", ">", "<=", ">=", "+", "-", "*", "/", "%", "^",
            "=", "+=", "-=", "/=", "%=", "|=", "^=",
            "!", "+", "-", "++", "--",
            ".", "$",
            //no use planned for these yet, but they are occasionally important in parsing (: is required for v2rv2)
            "@", "#", "?", ":", "~"
        }.ToHashSet();
        var operatorTrie = new Trie(operators.Concat(specialOps));
        var lexer = new RegexLexer<Token>(
            T(@"[^\S\n]+", TokenType.InlineWhitespace),
            T($@"{uLetter}({uLetter}|{num}|')*", (p, s) =>
                new Token(keywords.Contains(s.Value) ? TokenType.Keyword : TokenType.Identifier, p, s)),
            //Preprocess out other newlines
            T(@"\n", TokenType.Newline),
            T(@"[\(\)\[\]\{\},;]", (p, s) => new Token(s.Value switch {
                "(" => TokenType.OpenParen,
                ")" => TokenType.CloseParen,
                "[" => TokenType.OpenBracket,
                "]" => TokenType.CloseBracket,
                "{" => TokenType.OpenBrace,
                "}" => TokenType.CloseBrace,
                "," => TokenType.Comma,
                ";" => TokenType.Semicolon,
                _ => throw new ArgumentOutOfRangeException($"Not a special symbol: {s.Value}")
            }, p, s)),
            //123, 123.456, .456
            //Note that the preceding sign is parsed by Operator
            //This is to make basic cases like 5-6 vs 5+ -6 easier to handle
            T($@"(({num}+(\.{num}+)?)|(\.{num}+))({numMult})?", TokenType.Number),
            T(@"[!@#$%^&*+\-.<=>?/\\|~:]+", (p, s) => {
                var op = operatorTrie.FindLongestSubstring(s.Value);
                if (op is null) return Maybe<(Token, int)>.None;
                return (new Token(specialOps.Contains(op) ? TokenType.SpecialOperator : TokenType.Operator, 
                    p, op), op.Length);
            }),
            T(@"""([^""\\]+|\\([a-zA-Z0-9\\""'&]))*""", TokenType.String),
            
            //A block comment "fragment" is either a sequence of *s followed by a not-/ character, a sequence of not-*s.
            T(@"/\*((\*+[^/])|([^*]+))*\*/", TokenType.Comment),
            T(@"//[^\n]*", TokenType.Comment)
        );
        
        AssertHelpers.ListEq(lexer.Tokenize("12.5f--+ gorilla'++"), MakeFromSequence(
            (TokenType.Number, "12.5f"),
            (TokenType.Operator, "--"),
            (TokenType.Operator, "+"),
            (TokenType.InlineWhitespace, " "),
            (TokenType.Identifier, "gorilla'"),
            (TokenType.Operator, "++")
        ));
        
        AssertHelpers.ListEq(lexer.Tokenize("hello12"), MakeFromSequence((TokenType.Identifier, "hello12")));
        AssertHelpers.ListEq(lexer.Tokenize("hello 12"), MakeFromSequence(
            (TokenType.Identifier, "hello"), (TokenType.InlineWhitespace, " "), (TokenType.Number, "12")));
        //This will fail in the postprocessor because numbers+identifiers must be separated by whitespace.
        AssertHelpers.ListEq(lexer.Tokenize("12hello"), MakeFromSequence(
            (TokenType.Number, "12h"), (TokenType.Identifier, "ello")));

        var tokens = lexer.Tokenize(@"fn("")))""), )");

        int k = 5;
    }

    private static Token[] MakeFromSequence(params (TokenType type, string token)[] fragments) {
        var result = new Token[fragments.Length];
        var pos = new Position("", 0);
        for (int ii = 0; ii < fragments.Length; ++ii) {
            var (type, token) = fragments[ii];
            result[ii] = new(type, pos, token);
            pos = pos.Step(token, token.Length);
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