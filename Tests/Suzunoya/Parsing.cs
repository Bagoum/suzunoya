using System;
using System.Linq;
using NUnit.Framework;
using Suzunoya.Dialogue;
using static Tests.AssertHelpers;
using static Suzunoya.Dialogue.SpeechFragment;
using Char = Suzunoya.Dialogue.SpeechFragment.Char;

namespace Tests.Suzunoya {
public class Parsing {
    [Test]
    public void TestRolling() {
        var data = "heLlOWorLD";
        var cfg = SpeechSettings.Default with {
            opsPerSecond = 1,
            opsPerChar = (s, i) => 1,
            opsPerRollEvent = 3,
            rollEventAllowed = (s, i) => char.IsUpper(s[i]),
            rollEvent = () => { }
        };
        ListEq(new Speech(data, cfg).Fragments.Select(x => x.ToString()).ToArray(), new SpeechFragment[] {
            new Char('h'),
            new Wait(1),
            new Char('e'),
            new Wait(1),
            new RollEvent(cfg.rollEvent),
            new Char('L'),
            new Wait(1),
            new Char('l'),
            new Wait(1),
            new Char('O'),
            new Wait(1),
            new RollEvent(cfg.rollEvent),
            new Char('W'),
            new Wait(1),
            new Char('o'),
            new Wait(1),
            new Char('r'),
            new Wait(1),
            new RollEvent(cfg.rollEvent),
            new Char('L'),
            new Wait(1),
            new Char('D'),
            new Wait(1),
        }.Select(x => x.ToString()).ToArray());
    }
    
    [Test]
    public void TestTagNesting() {
        //Non-nested tags are possible, even with the same type, by using capitalization.
        var data = "a<speed=2>b<SPEED=4>c</speed>d</SPEED>efg";
        var cfg = SpeechSettings.Default with {
            opsPerSecond = 1,
            opsPerChar = (s, i) => 1,
            opsPerRollEvent = 1,
            rollEvent = null
        };

        var s1 = new Speech(data, cfg);
        ListEq(s1.TextUnits.Select(x => x.ToString()).ToArray(), new TextUnit[] {
            new TextUnit.String("a"),
            new TextUnit.OpenTag("speed", "2"),
            new TextUnit.String("b"),
            new TextUnit.OpenTag("SPEED", "4"),
            new TextUnit.String("c"),
            new TextUnit.CloseTag("speed"),
            new TextUnit.String("d"),
            new TextUnit.CloseTag("SPEED"),
            new TextUnit.String("efg"),
        }.Select(x => x.ToString()).ToArray());
        var t1 = new TagOpen("speed", new SpeechTag.Speed(2));
        var t2 = new TagOpen("SPEED", new SpeechTag.Speed(4));
        ListEq(s1.Fragments.Select(x => x.ToString()).ToArray(), new SpeechFragment[] {
            new Char('a'),
            new Wait(1),
            t1,
            new Char('b'),
            new Wait(0.5f),
            t2,
            new Char('c'),
            new Wait(0.125f),
            new TagClose(t1),
            new Char('d'),
            new Wait(0.25f),
            new TagClose(t2),
            new Char('e'),
            new Wait(1),
            new Char('f'),
            new Wait(1),
            new Char('g'),
            new Wait(1),
        }.Select(x => x.ToString()).ToArray());
        
        //The parser will prefer to match a closing tag to the last opened tag.
        data = "a<speed=2>b<speed=4>c</speed>d</speed>efg";
        var t3 = new TagOpen("speed", new SpeechTag.Speed(4));
        ListEq(new Speech(data, cfg).Fragments.Select(x => x.ToString()).ToArray(), new SpeechFragment[] {
            new Char('a'),
            new Wait(1),
            t1,
            new Char('b'),
            new Wait(0.5f),
            t3,
            new Char('c'),
            new Wait(0.125f),
            new TagClose(t3),
            new Char('d'),
            new Wait(0.5f),
            new TagClose(t1),
            new Char('e'),
            new Wait(1),
            new Char('f'),
            new Wait(1),
            new Char('g'),
            new Wait(1),
        }.Select(x => x.ToString()).ToArray());
    }
}
}