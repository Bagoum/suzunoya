using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using BagoumLib;
using BagoumLib.Functional;
using static Mizuhashi.Combinators;
using static Mizuhashi.Parsers.Markdown;
using BagoumLib.DataStructures;
#pragma warning disable 8851


namespace Mizuhashi.Parsers {
/// <summary>
/// A representation of a Markdown element.
/// </summary>
public abstract record Markdown {
    /// <summary>
    /// A top-level Markdown block that includes a tailing newline or EOF.
    /// </summary>
    public abstract record Block : Markdown {
        public virtual IEnumerable<Block> Flatten() => new[]{this};
        /// <summary>
        /// Convenience structure that contains two sequential blocks. This is used by internal
        ///  parsing handling, but will not be present in the results if you call Document or Parse.
        /// </summary>
        public record Paired(Block First, Block Second) : Block {
            public override IEnumerable<Block> Flatten() => First.Flatten().Concat(Second.Flatten());
        }

        /// <summary>
        /// Empty block.
        /// </summary>
        public record Empty : Block;
        
        /// <summary>
        /// A paragraph is a sequence of lines separated by single newlines. It cannot be empty.
        /// </summary>
        public record Paragraph(List<TextRun> Lines) : Block {
            public Paragraph(TextRun line) : this(new List<TextRun>() { line }) { }
            public Paragraph(TextRun line, Paragraph postceding) : this(postceding.Lines.Prepend(line).ToList()) { }
            
            public static Paragraph FromArray(params TextRun[] pieces) => new(pieces.ToList());
            public override string ToString() => $"<Paragraph {{ {string.Join(", ", Lines)} }}>";

            public virtual bool Equals(Paragraph? other) {
                if (other is null) return false;
                if (other.Lines.Count != this.Lines.Count) return false;
                for (int ii = 0; ii < Lines.Count; ++ii)
                    if (other.Lines[ii] != Lines[ii])
                        return false;
                return true;
            }
            
        }

        /// <summary>
        /// An ordered or unordered sequence of paragraphs displayed with bullets.
        /// </summary>
        public record List(bool Ordered, List<List<Block>> Lines) : Block {
            public override string ToString() {
                var orderPrefix = Ordered ? "O" : "Uno";
                var linesStr = string.Join("\n", Lines.Select(l => "- " + string.Join("\n", l)));
                return $"<{orderPrefix}rderedList{{\n\t{linesStr.Replace("\n", "\n\t")}\n}}>";
            }

            public virtual bool Equals(List? other) {
                if (other is null) return false;
                if (other.Lines.Count != this.Lines.Count) return false;
                for (int ii = 0; ii < Lines.Count; ++ii)
                    if (!other.Lines[ii].AreSame(Lines[ii]))
                        return false;
                return true;
            }

            public override IEnumerable<Block> Flatten() => new Block[] {
                new List(Ordered, Lines.Select(option =>
                        option.SelectMany(b => b.Flatten()).ToList())
                    .ToList())
            };
        }

        /// <summary>
        /// A code-block that may have a language tag.
        /// </summary>
        public record CodeBlock(string Language, string Contents) : Block;
        
        /// <summary>
        /// A header.
        /// </summary>
        /// <param name="Size">1-6, where 1 is the largest</param>
        /// <param name="Text">Header text</param>
        public record Header(int Size, TextRun Text) : Block;

        /// <summary>
        /// A horizontal rule.
        /// </summary>
        public record HRule : Block;

    }
    
    /// <summary>
    /// A sequence of Markdown text that contains at least one non-whitespace character.
    /// </summary>
    public abstract record TextRun {
        public record Sequence(List<TextRun> Pieces) : TextRun, IUnrollable<TextRun> {
            public static Sequence FromArray(params TextRun[] pieces) => new(pieces.ToList());
            public override string ToString() => $"<Sequence {{ {string.Join(", ", Pieces)} }}>";

            public virtual bool Equals(Sequence? other) {
                if (other is null) return false;
                if (other.Pieces.Count != this.Pieces.Count) return false;
                for (int ii = 0; ii < Pieces.Count; ++ii)
                    if (other.Pieces[ii] != Pieces[ii])
                        return false;
                return true;
            }

            public IEnumerable<TextRun> Values => Pieces;
        }

        public record Atom : TextRun {
            public Atom(string Text) {
                this.Text = Text;
            }
            public override string ToString() => $"\"{Text}\"";
            public string Text { get; init; }
            public void Deconstruct(out string Text) {
                Text = this.Text;
            }
        }

        public record Bold(TextRun Bolded) : TextRun;

        public record Italic(TextRun Italicized) : TextRun;

        public record InlineCode(string Text) : TextRun;

        public record Link(TextRun Title, string URL) : TextRun;

        public Sequence AsSeq() => new(new() { this });
        public static implicit operator TextRun(string s) => new Atom(s);
    }
    
    
}

/// <summary>
/// A Markdown parser. The top-level function is <see cref="MarkdownParser.Parse"/>.
/// <br/>Parsing Markdown is quite an annoyance, and there are a lot of ambiguities. It is highly likely that
/// there are imperfections in the handling here. Also, TODO: blockquotes, underscore-based bold/italics, and images
/// are not yet supported
/// </summary>
public static class MarkdownParser {
    public record Settings(int Indent = 0, int IndentBy = 2) {
        public Settings AddIndent => this with { Indent = Indent + 1 };
        private readonly ParserError.Expected err = new($"{Indent} indents");
        public Parser<char, Unit> ParseIndent => inp => 
            TrySkipIndent(inp.Source, inp.Index).Try(out var end) ?
                new(Unit.Default, null, inp.Index, inp.Step(end - inp.Index)) :
                new(err, inp.Index);

        public bool IsAligned(InputStream<char> inp) {
            for (int ii = inp.Index - 1; ii >= 0; --ii) {
                if (inp.Source[ii] == '\n')
                    return TrySkipIndent(inp.Source, ii + 1) == inp.Index;
            }
            return TrySkipIndent(inp.Source, 0) == inp.Index;
        }

        public int? TrySkipIndent(char[] source, int fromIndex) {
            var oddSpaces = 0;
            var indentsRead = 0;
            int ii = fromIndex;
            for (; ii < source.Length && indentsRead < Indent; ++ii) {
                if (!source.Try(ii, out var c) || c == '\n' || !char.IsWhiteSpace(c))
                    return null;
                if (c == '\t' || ++oddSpaces == IndentBy) {
                    ++indentsRead;
                    oddSpaces = 0;
                }
            }
            return (Indent == indentsRead && oddSpaces == 0) ? ii : null;
        }
        
        public int? TrySkipIndent(string source, int fromIndex) {
            var oddSpaces = 0;
            var indentsRead = 0;
            int ii = fromIndex;
            for (; ii < source.Length && indentsRead < Indent; ++ii) {
                if (!source.TryIndex(ii, out var c) || c == '\n' || !char.IsWhiteSpace(c))
                    return null;
                if (c == '\t' || ++oddSpaces == IndentBy) {
                    ++indentsRead;
                    oddSpaces = 0;
                }
            }
            return (Indent == indentsRead && oddSpaces == 0) ? ii : null;
        }

    }

    private static readonly ParserError ParensMismatched = new ParserError.Failure("Parentheses are mismatched.");
    /// <summary>
    /// Parses the URL link, assuming the opening ( has already been parsed.
    /// <br/>If it fails, it does not consume.
    /// </summary>
    private static readonly Parser<char, string> ParseURLLink = inp => {
        var openParens = 0;
        for (int ii = 0; inp.TryCharAt(ii, out var c) && c != '\n'; ++ii) {
            if (c == ')')
                if (openParens-- == 0)
                    return new ParseResult<string>(inp.Substring(ii), null, inp.Index, inp.Step(ii + 1));
            if (c == '(')
                ++openParens;
        }
        return new ParseResult<string>(ParensMismatched, inp.Index);
    };
    private static readonly ParserError TextRunEmpty = new ParserError.Expected("any text");
    private static readonly ParserError ExpectedEscaped = new ParserError.Expected("any escaped character");

    private abstract record TextContext {
        public List<Either<char, TextRun>> Parts { get; } = new();
        public abstract TextRun FailedResolve();

        public static TextRun.Sequence CompilePieces(List<Either<char, TextRun>> parts, char? startWith = null) {
            var compiled = new List<TextRun>();
            var lastStr = new StringBuilder();
            if (startWith != null)
                lastStr.Append(startWith.Value);
            void CompileLastString() {
                if (lastStr.Length > 0) {
                    compiled.Add(new TextRun.Atom(lastStr.ToString()));
                    lastStr.Clear();
                }
            }
            foreach (var p in parts)
                if (p.IsLeft)
                    lastStr.Append(p.Left);
                else {
                    CompileLastString();
                    compiled.Add(p.Right);
                }
            CompileLastString();
            return new TextRun.Sequence(compiled);
        }
        
        public record Asterisk : TextContext {
            public override TextRun FailedResolve() => CompilePieces(Parts, '*');
            public TextRun ResolveItalic() => new TextRun.Italic(CompilePieces(Parts));

            public TextRun ResolveBold(Asterisk inner) => new TextRun.Bold(
                new TextRun.Sequence(CompilePieces(Parts).Pieces.Concat(CompilePieces(inner.Parts).Pieces).ToList()));
        }
        public record Bracket : TextContext {
            public override TextRun FailedResolve() => CompilePieces(Parts, '[');
            public TextRun Resolve(string linkURL) => new TextRun.Link(CompilePieces(Parts), linkURL);
        }
    }
    public static readonly Parser<char, TextRun> ParseTextRun = inp => {
        var start = inp.Index;
        List<Either<char, TextRun>>? topLevelParts = null;
        StackList<TextContext>? contexts = null;
        void AddPiece(Either<char, TextRun> tr) {
            if (contexts?.MaybePeek() != null)
                contexts.Peek().Parts.Add(tr);
            else
                (topLevelParts ??= new()).Add(tr);
        }
        void AddContext(TextContext tc) => (contexts ??= new()).Push(tc);
        bool allWhitespace = true;

        while (!inp.Empty && inp.Next != '\n') {
            allWhitespace &= char.IsWhiteSpace(inp.Next);
                
            if (inp.Next == '\\') {
                inp.Step();
                if (!inp.Empty) {
                    AddPiece(new(inp.Next));
                    inp.Step();
                } else
                    return new(ExpectedEscaped, start, inp.Index);
            }
            if (inp.Next == '`') {
                inp.Step();
                for (int ii = 0; inp.TryCharAt(ii, out var c) && c != '\n'; ++ii) {
                    if (c == '`') {
                        AddPiece(new(new TextRun.InlineCode(inp.Substring(ii))));
                        inp.Step(ii + 1);
                        goto success;
                    }
                }
                AddPiece(new('`'));
                success: ;
            } else if (inp.Next == ']' && contexts?.Any(c => c is TextContext.Bracket) is true) {
                while (contexts.Peek() is not TextContext.Bracket)
                    AddPiece(new(contexts.Pop().FailedResolve()));
                var b = (contexts.Pop() as TextContext.Bracket)!;
                if (inp.TryCharAt(1, out var c) && c == '(') {
                    inp.Step(2);
                    var res = ParseURLLink(inp);
                    if (res.Result.Valid)
                        AddPiece(new(b.Resolve(res.Result.Value)));
                    else {
                        AddPiece(new(b.FailedResolve()));
                        AddPiece(new(']'));
                        AddPiece(new('('));
                    }
                } else
                    AddPiece(new(b.FailedResolve()));
            } else if (inp.Next == '[') {
                inp.Step();
                AddContext(new TextContext.Bracket());
            } else if (inp.Next == '*') {
                inp.Step();
                var numAsterisks = contexts?.Count(c => c is TextContext.Asterisk) ?? 0;
                if (contexts == null || numAsterisks == 0)
                    AddContext(new TextContext.Asterisk());
                else {
                    //Push if there are 1 or 2 adjacent asterisks, but not if there are 3
                    if (contexts.Peek() is TextContext.Asterisk a1 && a1.Parts.Count == 0) {
                        if (contexts.Count > 1 && contexts.Peek(2) is TextContext.Asterisk a2 && a2.Parts.Count == 0)
                            if (contexts.Count > 2 && contexts.Peek(3) is TextContext.Asterisk a3 && a3.Parts.Count == 0)
                                goto checkNesting;
                        AddContext(new TextContext.Asterisk());
                        continue;
                    }
                    checkNesting:
                    bool canCloseBold = !inp.Empty && inp.Next == '*';
                    //If within a brackets context and there is a closing asterisk before a closing bracket, then push
                    if (contexts.Peek() is TextContext.Bracket) {
                        bool foundAsterisk = false;
                        for (int jj = 1; inp.TryCharAt(jj, out var lf); ++jj) {
                            foundAsterisk |= lf == '*';
                            if (lf == ']') {
                                if (foundAsterisk) {
                                    AddContext(new TextContext.Asterisk());
                                    break;
                                } else
                                    goto breakContexts;
                            }
                        }
                        continue;
                    }
                    breakContexts:
                    //Break all contexts (specifically brackets) moving downwards
                    while (contexts.Peek() is not TextContext.Asterisk)
                        AddPiece(new(contexts.Pop().FailedResolve()));
                    var ast1 = (contexts.Pop() as TextContext.Asterisk)!;
                    if (contexts.MaybePeek() is TextContext.Asterisk a) {
                        if (canCloseBold && a.Parts.Count == 0) {
                            inp.Step();
                            contexts.Pop();
                            AddPiece(new(a.ResolveBold(ast1)));
                        } else if (contexts.Count > 1 && contexts.Peek(2) is TextContext.Asterisk aOuter && aOuter.Parts.Count == 0) {
                            //***text* -> match 2(ast1) and 3(this)
                            AddPiece(new(ast1.ResolveItalic()));
                        } else {
                            //**text* -> match 0(a) and 2(this), fail 1(ast1)
                            AddPiece(new(ast1.FailedResolve()));
                            AddPiece(new((contexts.Pop() as TextContext.Asterisk)!.ResolveItalic()));
                        }
                    } else
                        AddPiece(new(ast1.ResolveItalic()));
                }
            } else {
                AddPiece(new(inp.Next));
                inp.Step();
            }
        }
        if (allWhitespace)
            return new ParseResult<TextRun>(TextRunEmpty, start, inp.Index);
        while (contexts?.MaybePeek() != null)
            AddPiece(new(contexts.Pop().FailedResolve()));
        if (topLevelParts is { Count: > 0 })
            return new ParseResult<TextRun>(TextContext.CompilePieces(topLevelParts), null, start, inp.Index);
        else
            return new ParseResult<TextRun>(TextRunEmpty, start, inp.Index);

    };

    private static readonly ParserError.Expected expectedWhitespaceNL = new ("whitespace followed by newline");
    /// <summary>
    /// Returns true if there exists a newline, false if there exists an EOF, after any amount of inline whitespace.
    /// </summary>
    public static readonly Parser<char, bool> ParseNLOrEOF = inp => {
        int ii = 0;
        for (; inp.TryCharAt(ii, out var c); ++ii) {
            if (c == '\n')
                return new(true, null, inp.Index, inp.Step(ii + 1));
            if (!char.IsWhiteSpace(c))
                return new(expectedWhitespaceNL, inp.Index);
        }
        return new(false, null, inp.Index, inp.Step(ii));
    };
    private static Parser<char, string> ParseNLiner(char c, int n) {
        var err = new ParserError.Expected($"at least {n} copies of {c} followed by whitespace and newline");
        return inp => {
            int ii = 0;
            for (; inp.TryCharAt(ii, out var ch) && ch == c; ++ii) {}
            if (ii >= n) {
                for (; inp.TryCharAt(ii, out var ch); ++ii) {
                    if (ch == '\n')
                        return new(inp.Substring(ii), null, inp.Index, inp.Step(ii + 1));
                    if (!char.IsWhiteSpace(ch))
                        return new(err, inp.Index);
                }
                return new(inp.Substring(ii), null, inp.Index, inp.Step(ii));
            } else return new(err, inp.Index);
        };
    }

    /// <summary>
    /// The parser will NOT look for whitespace at the start corresponding to indent.
    /// If you need to enforce whitespace, then do Spaces(indent).IgThen(block(indent)).
    /// <br/>The parser WILL consume the newline that ends the block.
    /// </summary>
    public static Parser<char, Block> ParseBlock(Settings s) => 
        Choice(
            //empty
            ParseNLOrEOF.Bind(b => b ? PReturn<char, Block>(new Block.Empty()) : Error<char, Block>("EOF")),
            
            //Code block
            Sequential(
                StringIg("```"),
                ManySatisfy(c => c != '\n', false, "language description"),
                Newline.ThenIg(s.ParseIndent),
                //If in the first column aligned with the indent there are three consecutive backticks,
                // we end the block.
                ManySatisfyI(stream => !s.IsAligned(stream) || stream.Next != '`' ||
                                       !stream.TryCharAt(1, out var l1) || l1 != '`' ||
                                       !stream.TryCharAt(2, out var l2) || l2 != '`'),
                StringIg("```").IgThen(ParseNLOrEOF),
                (_, lang, _, contents, _) => {
                    var sb = new StringBuilder();
                    for (int ii = 0; ii < contents.Length;) {
                        sb.Append(contents[ii]);
                        if (contents[ii] == '\n') {
                            ii = s.TrySkipIndent(contents, ii + 1) ?? ii + 1;
                        } else
                            ++ii;
                    }
                    contents = sb.ToString();
                    //remove last newline
                    for (int ii = 1; ii < contents.Length; ++ii) {
                        if (contents[^ii] == '\n') {
                            contents = contents[..^ii];
                            break;
                        }
                    }
                    return new Block.CodeBlock(lang, contents) as Block;
                }),
            
            //Header (using #)
            Many1Satisfy(c => c == '#', "#").ThenIg(Satisfy(char.IsWhiteSpace, "whitespace"))
                .Then(ParseTextRun).ThenIg(ParseNLOrEOF).Bind(tuple => 
                tuple.Item1.Length > 6 ? 
                    Error<char, Block>($"headers can have a maximum 6 hashtags, found {tuple.Item1.Length}") :
                    PReturn<char, Block>(new Block.Header(tuple.Item1.Length, tuple.Item2))
            ).Attempt(),
            
            //Horizontal rule
            Choice(ParseNLiner('*', 3), ParseNLiner('-', 3), ParseNLiner('_', 3))
                .ThenPReturn(new Block.HRule() as Block),
            
            //List
            ParseListOptions(false, s).FMap(entries => new Block.List(false, entries) as Block)
                .Label($"unordered list options at indent {s.Indent}"),
            ParseListOptions(true, s).FMap(entries => new Block.List(true, entries) as Block)
                .Label($"ordered list options at indent {s.Indent}"),

            //Header (using === or ---) or paragraph
            ParseTextRun.Then(ParseNLOrEOF)
                .Bind(((TextRun line1, bool nl) x) => {
                    Block onelinePara = new Block.Paragraph(x.line1);
                    return !x.nl ? 
                        //If we are at EOF, then the paragraph is complete
                        PReturn<char, Block>(onelinePara) :
                        Choice(
                            //If the next line has no content, then the paragraph is complete
                            ParseNLOrEOF.ThenPReturn(onelinePara),
                            //Try a continuation with spaces
                            s.ParseIndent.IgThen(Choice(
                                //If the next line is ==+, return header 1
                                ParseNLiner('=', 2).FMap(_ => new Block.Header(1, x.line1) as Block),
                                //If the next line is --+, return header 2
                                ParseNLiner('-', 2).FMap(_ => new Block.Header(2, x.line1) as Block),
                                //Get the next block
                                ParseBlock(s).FMap(b => b switch {
                                    //If it's a paragraph, then join the paragraphs
                                    Block.Paragraph para => new Block.Paragraph(x.line1, para) as Block,
                                    Block.Paired pair => pair.First is Block.Paragraph para ?
                                        new Block.Paired(new Block.Paragraph(x.line1, para), pair.Second) :
                                        new Block.Paired(onelinePara, pair),
                                    //Otherwise, just join the blocks
                                    _ => new Block.Paired(onelinePara, b)
                                })
                            )),
                            //Otherwise, assume a dedent broke the current context
                            PReturn<char, Block>(onelinePara)
                        );
                })
        );

    private static Parser<char, List<List<Block>>> ParseListOptions(bool ordered, Settings s) {
        var err = new ParserError.Expected("at least one list entry");
        Parser<char, List<Block>>? lazyBlocksParser = null;
        var prefix = ordered ? 
            Many1Satisfy(char.IsDigit, "digit").ThenIg(StringIg(". ")).Attempt().Ignore() : 
            StringIg("- ");
        var parser = prefix.Bind(_ => lazyBlocksParser ??= ParseManyBlocks(s.AddIndent));
        return input => {
            var start = input.Index;
            List<List<Block>> results = null!;
            List<List<Block>> Results() => results ??= new();
            ParseResult<List<List<Block>>> Finalize<T>(ParseResult<T> last) => 
                results?.Count > 0 ?
                    new(Results(), null, start, last.End) :
                    new(err, start, input.Index);
            while (true) {
                if (results?.Count > 0) {
                    var checkSpaces = s.ParseIndent(input);
                    if (!checkSpaces.Result.Valid)
                        return Finalize(checkSpaces);
                }
                var next = parser(input);
                if (next.Status == ResultStatus.FATAL)
                    return next.CastFailure<List<List<Block>>>();
                else if (next.Status == ResultStatus.ERROR)
                    return Finalize(next);
                else if (!next.Consumed)
                    return new(
                        new ParserError.Failure("`ManyBlocks` parser parsed an object without consuming text."), next.Start);
                else
                    Results().Add(next.Result.Value);
            }
        };
    }

    private static Parser<char, List<Block>> ParseManyBlocks(Settings s) => input => {
        var results = new List<Block>();
        var start = input.Index;
        var b = ParseBlock(s);
        ParseResult<List<Block>> Finalize<T>(ParseResult<T> last) => new(results, null, start, last.End); 
        while (true) {
            var checkEmpty = ParseNLOrEOF(input);
            if (checkEmpty.Result.Valid) {
                if (checkEmpty.Result.Value) {
                    results.Add(new Block.Empty());
                    continue;
                } else
                    return Finalize(checkEmpty);
            }
            if (results.Count > 0) {
                var checkSpaces = s.ParseIndent(input);
                if (!checkSpaces.Result.Valid)
                    return Finalize(checkSpaces);
            }
            var next = b(input);
            if (next.Status == ResultStatus.FATAL)
                return next.CastFailure<List<Block>>();
            else if (next.Status == ResultStatus.ERROR)
                return Finalize(next);
            else if (!next.Consumed)
                return new(
                    new ParserError.Failure("`ManyBlocks` parser parsed an object without consuming text."), next.Start);
            else
                results.Add(next.Result.Value);
        }
    };

    private static IEnumerable<Block> Reformat(IEnumerable<Block> bs) {
        Block? prev = null;
        foreach (var b in bs.SelectMany(b => b.Flatten())) {
            //headers/hrule can consume one preceding empty
            if (b is Block.Header or Block.HRule &&
                prev is Block.Empty) {
                prev = null;
            }
            if (prev != null)
                yield return prev;
            //and one postceding empty
            if (b is Block.Empty && prev is Block.Header or Block.HRule) {
                prev = null;
            } else
                prev = b;
        }
        if (prev != null)
            yield return prev;
    }
    public static Parser<char, List<Block>> ParseDocument(Settings s) =>
        ParseManyBlocks(s).ThenEOF().FMap(bs => Reformat(bs).ToList());

    /// <summary>
    /// Parse a Markdown document.
    /// </summary>
    /// <param name="markdownText">Markdown document as raw text</param>
    /// <param name="settings">Indentation settings (defaults to an indent of 2)</param>
    /// <returns>A list of Markdown blocks constituting the entire document. There is a possibility
    /// that this throws an exception, but it *should be* impossible for parsing to fail.</returns>
    public static List<Block> Parse(string markdownText, Settings? settings = null) {
        markdownText = markdownText.Replace("\r\n", "\n").Replace("\r", "\n");
        return ParseDocument(settings ?? new())
            .ResultOrErrorString(new("Markdown text", markdownText.ToCharArray(), null!, 
                new CharTokenWitnessCreator(markdownText)))
            .Map(l => l, err => throw new Exception(err));
    }
}
}