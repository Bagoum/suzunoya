using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BagoumLib.Functional;
using JetBrains.Annotations;

namespace Mizuhashi.Lexers {
/// <summary>
/// A handler for creating a token from a source string using regex parsing.
/// </summary>
/// <param name="RegexPattern">A regex pattern describing the token. The lexer will automatically prepend \G to this pattern to ensure that it generates contiguous tokens.</param>
/// <param name="Tokenizer">A function that converts a regex match from RegexPattern into a token, with the option of failing and forcing a different match. Returns the token and the length of the match that was actually used.</param>
/// <typeparam name="T">Type of result token.</typeparam>
public record RegexTokenizer<T>([RegexPattern] string RegexPattern, Func<Position, Match, Maybe<(T token, int consumed)>> Tokenizer) {
    /// <summary>
    /// Description for this regex.
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// Flags for regex construction. By default, uses RegexOptions.Compiled.
    /// </summary>
    public RegexOptions Flags { get; init; } = RegexOptions.Compiled; //NonBacktracking would be useful, but its .NET7 only

    public RegexTokenizer(string regexPattern, Func<Position, Match, Maybe<T>> tokenizer) : this(regexPattern,
        (p, m) => {
            var token = tokenizer(p, m);
            if (token.Valid)
                return (token.Value, m.Length);
            return Maybe<(T, int)>.None;
        }) { }

}
/// <summary>
/// A simple lexer that uses regexes to convert an input string into a list of tokens.
/// <br/>This does not have support for indentation, as that requires a parser stronger than regex.
/// </summary>
public class RegexLexer<T> {
    private readonly RegexTokenizer<T>[] tokenizers;
    //We separate regexes in order to handle regex priority.
    // In a single joined regex (ie. (option1)|(option2)|(option3)...), it is not trivial
    // to handle cases where certain regexes ought to have priority over others.
    private readonly Regex[] regexes;
    private readonly Regex regex;
    private readonly string[] groupNames;

    /// <summary>
    /// Create a regex-based lexer.
    /// </summary>
    /// <param name="tokenizers">Token regexes, defined in decreasing order of priority.</param>
    public RegexLexer(params RegexTokenizer<T>[] tokenizers) {
        this.tokenizers = tokenizers;
        //\G is like ^, except it also works when you use regex.Match(str, startFromIndex).
        //See https://learn.microsoft.com/en-us/dotnet/standard/base-types/anchors-in-regular-expressions
        regexes = tokenizers.Select(t => new Regex($"\\G{t.RegexPattern}", t.Flags)).ToArray();
        groupNames = new string[tokenizers.Length];
        regex = new Regex($"\\G({string.Join("|", tokenizers.Select((h, i) => $"(?<{groupNames[i] = $"regexLexerGroup{i}"}>{h.RegexPattern})"))})",
            RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.Compiled);
    }

    /// <summary>
    /// Convert a source string into a list of contiguous tokens.
    /// </summary>
    /// <exception cref="Exception">Throws an exception when the string could not be tokenized.</exception>
    public List<T> Tokenize(string source) {
        var tokens = new List<T>();
        var prevIndex = 0;
        var position = new Position(source, 0);
        for (int index = 0; index < source.Length;) {
            //Multiple regex implementation (somewhat slower)
            for (int it = 0; it < tokenizers.Length; ++it) {
                var match = regexes[it].Match(source, index);
                if (match.Success) {
                    var token = tokenizers[it].Tokenizer(position, match);
                    if (token.Try(out var t)) {
                        prevIndex = index;
                        index += t.consumed;
                        position = position.Step(match.Value, t.consumed);
                        tokens.Add(t.token);
                        goto nextLoop;
                    }
                }
            }
            var pos = new Position(source, index);
            var sb = new StringBuilder();
            sb.Append($"Failed to tokenize the source data at {new Position(source, index)}:\n");
            sb.Append(pos.PrettyPrintLocation(source));
            if (tokens.Count > 0)
                sb.Append(
                    $"\nThe most recently parsed token was {tokens[^1]} at {new PositionRange(new(source, prevIndex), new(source, index))}.");
            throw new Exception(sb.ToString());
            nextLoop: ;
        }
        return tokens;
    }
}
}