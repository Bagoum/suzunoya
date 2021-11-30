using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BagoumLib;
using BagoumLib.Culture;
using Suzunoya.Data;
using static Suzunoya.Dialogue.SpeechTag;

namespace Suzunoya.Dialogue {
public class Speech {
    private readonly LString raw;
    private readonly SpeechSettings cfg;
    
    private List<TextUnit>? textUnits;
    public List<TextUnit> TextUnits => textUnits ??= SpeechParser.Parse(raw).GetOrThrow;
    private List<SpeechFragment>? fragments;
    public List<SpeechFragment> Fragments => fragments ??= Parse(cfg, TextUnits);
    private string? readable;
    public string Readable => readable ??= ComputeReadable();

    public Speech(LString raw, ISettings? settings, SpeechSettings? cfg = null) {
        this.raw = raw;
        this.cfg = (cfg ??= SpeechSettings.Default) with {
            opsPerSecond = cfg.opsPerSecond * (settings?.TextSpeed ?? 1)
        };
    }
    
    //Note for tag parsing: 
    //<b>a<color=red>a</b>a</color>a
    //is legal in TMP and should be legal here.


    private string ComputeReadable() {
        var sb = new StringBuilder();
        foreach (var frag in Fragments) {
            if (frag is SpeechFragment.Char s)
                sb.Append(s.fragment);
        }
        return sb.ToString();
    }

    private static List<SpeechFragment> Parse(SpeechSettings settings, IReadOnlyList<TextUnit> units) {
        var opsUntilNextRollEvent = 0f;
        var frags = new List<SpeechFragment>();
        var cfg_stack = new Stack<SpeechSettings>();
        cfg_stack.Push(settings);
        var tag_stack = new Stack<SpeechFragment.TagOpen>();
        var tmp_tag_pop = new Stack<SpeechFragment.TagOpen>();

        for (int ii = 0; ii < units.Count; ++ii) {
            var cfg = cfg_stack.Peek();
            var unit = units[ii];
            if (unit is TextUnit.String s) {
                for (int ic = 0; ic < s.fragment.Length; ++ic) {
                    if ((opsUntilNextRollEvent -= cfg.opsPerChar(s.fragment, ic)) <= 0 
                        && cfg.rollEventAllowed(s.fragment, ic) && cfg.rollEvent != null) {
                        frags.Add(new SpeechFragment.RollEvent(cfg.rollEvent));
                        opsUntilNextRollEvent = cfg.opsPerRollEvent;
                    }
                    frags.Add(new SpeechFragment.Char(s.fragment[ic]));
                    var waitTime = cfg.opsPerChar(s.fragment, ic) / cfg.opsPerSecond;
                    if (waitTime > 0)
                        frags.Add(new SpeechFragment.Wait(waitTime));
                }
            } else if (unit is TextUnit.OpenTag ot) {
                var t = new SpeechFragment.TagOpen(ot.name, ToTag(ot.name, ot.content));
                frags.Add(t);
                tag_stack.Push(t);
                cfg_stack.Push(t.tag.ModifySettings(cfg));
            } else if (unit is TextUnit.CloseTag ct) {
                //Undo tags until the matching opening tag is found
                while (tag_stack.TryPeek()?.name != ct.name) {
                    if (tag_stack.Count == 0)
                        throw new Exception($"Could not find a matching opening tag for {ct.name}");
                    tmp_tag_pop.Push(tag_stack.Pop());
                    cfg_stack.Pop();
                }
                frags.Add(new SpeechFragment.TagClose(tag_stack.Pop()));
                cfg_stack.Pop();
                //Reapply tags that were popped
                while (tmp_tag_pop.Count > 0) {
                    var t = tmp_tag_pop.Pop();
                    tag_stack.Push(t);
                    cfg_stack.Push(t.tag.ModifySettings(cfg_stack.Peek()));
                }
            }
        }
        return frags;
    }

    private static SpeechTag ToTag(string name, string? content) => name.ToLower() switch {
        //NOTE: InvariantCulture is CRITICAL here! Otherwise this won't work in comma-decimal countries!
        "speed" => float.TryParse(content ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ?
            new Speed(f) :
            throw new Exception($"Speed tag requires a float value. Couldn't parse given {f}"),
        "silent" => new Silent(),
        "color" or "fontcolor" => new Color(content ?? "#ffffff"),
        "furigana" or "furi" or "ruby" => new Furigana(content ?? ""),
        _ => new Unknown(name, content)
    };

}

public abstract class SpeechFragment {
    public class Char : SpeechFragment {
        public readonly char fragment;
        public Char(char fragment) {
            this.fragment = fragment;
        }

        public override string ToString() => $"Char {{ fragment = {fragment} }}";
    }

    public class Wait : SpeechFragment {
        public readonly float time;
        public Wait(float time) {
            this.time = time;
        }
    }

    //change to internal management type
    public class RollEvent : SpeechFragment {
        public readonly Action ev;
        public RollEvent(Action ev) {
            this.ev = ev;
        }
    }

    //Tags can be used to modify SpeakSettings, eg. <speed=*2>fast text!</speed>
    public class TagOpen : SpeechFragment {
        public readonly string name;
        public readonly SpeechTag tag;
        public TagOpen(string name, SpeechTag tag) {
            this.name = name;
            this.tag = tag;
        }

        public override string ToString() => $"TagOpen {{ name = {name}, tag = {tag} }}";
    }

    public class TagClose : SpeechFragment {
        public readonly TagOpen opener;
        public TagClose(TagOpen opener) {
            this.opener = opener;
        }
        
        public override string ToString() => $"TagClose {{ opener = {opener} }}";
    }
}


}