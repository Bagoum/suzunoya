using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Functional;
using Mizuhashi;
using Mizuhashi.Parsers;
using NUnit.Framework;
using NUnit.Framework.Internal;
using static Mizuhashi.Parsers.Markdown;
using static Mizuhashi.Parsers.Markdown.TextRun;
using static Mizuhashi.Parsers.Markdown.Block;
using static Mizuhashi.Parsers.MarkdownParser;
using static Mizuhashi.ParserError;
using static BagoumLib.IEnumExtensions;

namespace Tests.Mizuhashi {

public class TestMarkdown {

    private static Settings s = new Settings();
    private static TextRun Flatten(TextRun tr) => tr switch {
        Sequence seq => new Sequence(seq.Values.Unroll().Select(Flatten).ToList()),
        Italic i => new Italic(Flatten(i.Italicized)),
        Bold b => new Bold(Flatten(b.Bolded)),
        Link l => new Link(Flatten(l.Title), l.URL),
        _ => tr
    };
    private static readonly Parser<char, TextRun> TextRunF = MarkdownParser.ParseTextRun.FMap(Flatten);
    
    private static Paragraph P(params TextRun[] lines) => Paragraph.FromArray(lines);
    private static Sequence S(params TextRun[] lines) => Sequence.FromArray(lines);
    
    [Test]
    public void TestTextRun() {
        TextRunF.AssertSuccess("hello\nworld", new Atom("hello").AsSeq());
        TextRunF.AssertSuccess("hel*lo\nworld", new Sequence(new(){new Atom("hel"), new Atom("*lo")}));
        TextRunF.AssertSuccess("he*l*lo\nworld", new Sequence(new(){new Atom("he"), 
            new Italic(new Atom("l").AsSeq()), new Atom("lo")}));
        TextRunF.AssertSuccess("normal** bolded ** normal", S(new Atom("normal"), 
            new Bold(new Atom(" bolded ").AsSeq()), new Atom(" normal")));
        TextRunF.AssertSuccess("normal*** bold*ed ** normal", S(new Atom("normal"), 
            new Bold(S(new Italic(new Atom(" bold").AsSeq()), new Atom("ed "))), new Atom(" normal")));
        //third asterisk closes the first one
        TextRunF.AssertSuccess("normal** *bold*ed ** normal", S(new Atom("normal"), 
            new Italic(S(new Atom("* "))), new Atom("bold"), 
            new Italic(S(new Atom("ed "))),
            new Atom("* normal")));

        TextRunF.AssertFail("", new Expected("any text"));
        //Whitespace is allowed, but it can't be all whitespace
        TextRunF.AssertSuccess("h   ", new Atom("h   ").AsSeq());
        TextRunF.AssertFail("   ", new Expected("any text"));
        TextRunF.AssertFail("\n", new Expected("any text"));
        
    }

    [Test]
    public void TestTextRunURL() {
        TextRunF.AssertSuccess("**bold[bold*italic*bold](url)bold**", S(new Bold(S(
                "bold", new Link(S("bold", new Italic(S("italic")), "bold"), "url"), "bold"
            ))));
        TextRunF.AssertSuccess("[normal*normal](url)", S(new Link(
            S("normal", "*normal"), "url")));
        TextRunF.AssertSuccess("[normal **bold** normal](url)", S(new Link(
            S("normal ", new Bold(S("bold")), " normal"), "url" 
        )));
        TextRunF.AssertSuccess("*[normal `code*` normal](url)", S(
            "*", new Link(
                S("normal ", new InlineCode("code*"), " normal"), "url" 
        )));
        TextRunF.AssertSuccess("[normal **bold** normal](url", 
            S("[normal ", new Bold(S("bold")), " normal", "](url")
        );
        TextRunF.AssertSuccess("[normal **bold** normal](url\n)", 
            S("[normal ", new Bold(S("bold")), " normal", "](url")
        );
        TextRunF.AssertSuccess("*[italic*normal](url", 
            S(
                new Italic(S("[italic")), 
                "normal](url")
        );
    }

    [Test]
    public void TestHeader() {
        ParseBlock(s).AssertSuccess("hello   \n===", new Header(1, S("hello   ")));
        ParseBlock(s).AssertSuccess("hello   \n===w", 
            P(S("hello   "), S("===w")));
        ParseBlock(s).AssertSuccess("### hello   \nworld", new Header(3, S("hello   ")));
        ParseBlock(s).AssertSuccess("###   ", P(S("###   ")));
        ParseBlock(s).AssertSuccess("###hello   \nworld", 
            P(S("###hello   "), S("world")));
    }

    private static Parser<char, List<Block>> ParseDocument(Settings s) => inp =>
        MarkdownParser.ParseDocument(s)(new(new string(inp.Source).Replace("\r\n", "\n").Replace("\r", "\n"), 
            inp.Description, inp.Stative.State));

    [Test]
    public void TestParagraph() {
        ParseDocument(s).AssertSuccess("hello\nworld\n\nfoo\nbar", new() {
            P(S("hello"), S("world")),
            P(S("foo"), S("bar"))
        });
        ParseDocument(s).AssertSuccess(@"```my language
hello
 world
```", new(){new CodeBlock("my language", "hello\n world")});
        //even with a 1-line separator, paragraphs won't absorb anything but basic text
        ParseDocument(s).AssertSuccess(@"hello
- option 1
", new() {
            P(S("hello")),
            new Block.List(false, new() {
                new() {P(S("option 1")) }
            })
        });
    }

    [Test]
    public void TestList() {
        ParseBlock(s).AssertSuccess(@"- a
- b
  b2

- 
  c
d", new Block.List(false, new() {
            new() { P(S("a")) },
            new() {P(S("b"), S("b2"))},
            new(){new Empty(), P(S("c"))}
        }));
        
        ParseBlock(s).AssertSuccess(@"5. a
6. b
  b2

7.  
  c
d", new Block.List(true, new() {
            new() { P(S("a")) },
            new() {P(S("b"), S("b2"))},
            new(){new Empty(), P(S("c"))}
        }));
        
        ParseDocument(s).AssertSuccess(@"- option 1

- 


  red
  
  blue
  ===
  
  ```js
  hello
  world
  ```
unindented", new() {
            new Block.List(false, new() {
                new() {
                    P(S("option 1"))
                },
                new() {
                    new Empty(),
                    new Empty(),
                    new Empty(),
                    P(S("red")),
                    new Header(1, S("blue")),
                    new Empty(),
                    new CodeBlock("js", "hello\nworld")
                }
            }),
            P(S("unindented"))
        });
        
        ParseDocument(s).AssertSuccess(@"- option 1

14. 


  red
  
  blue
  ===
  
  ```js
  hello
  world
  ```
unindented", new() {
            new Block.List(false, new() {
                new() {
                    P(S("option 1"))
                }}),
                new Block.List(true, new() {
                new() {
                    new Empty(),
                    new Empty(),
                    new Empty(),
                    P(S("red")),
                    new Header(1, S("blue")),
                    new Empty(),
                    new CodeBlock("js", "hello\nworld")
                }}),
            P(S("unindented"))
        });
        
        
        ParseDocument(s).AssertSuccess(@"- option 1
a
- option 2", new() {
            new Block.List(false, new() {
                new() { P(S("option 1"))}
            }),
            P(S("a")),
            new Block.List(false, new() {
                new() { P(S("option 2"))}
            }),
        });
        ParseDocument(s).AssertSuccess(@"6. option 1
a
12. option 2", new() {
            new Block.List(true, new() {
                new() { P(S("option 1"))}
            }),
            P(S("a")),
            new Block.List(true, new() {
                new() { P(S("option 2"))}
            }),
        });
    }

    [Test]
    public void TestNestedList() {
        ParseDocument(s).AssertSuccess(@"
- - a
  - b", new() {
            new Empty(),
            new Block.List(false, new() {
                new() { new Block.List(false, new() {
                    new() { P(S("a"))},
                    new() { P(S("b"))},
                })}
            })
        });
        ParseDocument(s).AssertSuccess(@"
- 1. a
  2. b", new() {
            new Empty(),
            new Block.List(false, new() {
                new() { new Block.List(true, new() {
                    new() { P(S("a"))},
                    new() { P(S("b"))},
                })}
            })
        });
        
        ParseDocument(s).AssertSuccess(@"- a
- 1. b1
  2. b2
  3. - b3a
    - b3b
       b3bb
  4. b4
  5. - b5a
- c", new() {
            new Block.List(false, new() {
                new() { P(S("a"))},
                new() {
                    new Block.List(true, new() {
                        new() { P(S("b1"))},
                        new(){P(S("b2"))},
                        new () {
                            new Block.List(false, new() {
                                new() { P(S("b3a"))},
                                new() { P(S("b3b"), S(" b3bb"))}
                            })
                        },
                        new() {P(S("b4"))},
                        new() {
                            new Block.List(false, new() {
                                new() { P(S("b5a"))}
                            })
                        }
                    })
                },
                new() { P(S("c"))}
            })
        });
    }

    [Test]
    public void TestEmpty() {
        ParseDocument(s).AssertSuccess("", new ());
        ParseDocument(s).AssertSuccess("   ", new());
        ParseDocument(s).AssertSuccess("   \n", new() {new Empty()});
        ParseDocument(s).AssertSuccess("   \n   ", new() {new Empty()});
        ParseDocument(s).AssertSuccess("   \n   \n", new() {new Empty(), new Empty()});
        //paragraphs will absorb up to 2 newlines
        ParseDocument(s).AssertSuccess("hello   \n  ", new() {P(S("hello   "))});
        ParseDocument(s).AssertSuccess("hello   \n   \n  ", new() {P(S("hello   "))});
        ParseDocument(s).AssertSuccess("hello   \n   \n  \n  ", new() {P(S("hello   ")), new Empty()});
    }

    /*
    [Test]
    public void TestEXT() {
        Console.WriteLine(Environment.CurrentDirectory);
        var txt = File.ReadAllText("test1.md");
        var md = MarkdownParser.Parse(txt);
        var k = 5;
    }*/

}
}