using System;
using System.Collections;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Suzunoya.ControlFlow;
using Suzunoya.Dialogue;

namespace Suzunoya.Entities {
[Flags]
public enum SpeakFlags {
    None = 0,
    /// <summary>
    /// By default, every text command will reset all visible text. To avoid this,
    /// use this flag (or call AlsoSay).
    /// </summary>
    DontClearText = 1 << 0,
    /// <summary>
    /// Don't show the name, image, etc of the speaker on the dialogue box. This should be
    /// utilized by the mimic class.
    /// </summary>
    Anonymous = 1 << 1,
    
    Default = None,
}

public interface IDialogueBox : IEntity {
    AccEvent<(SpeechFragment frag, string lookahead)> Dialogue { get; }
    Event<Unit> DialogueCleared { get; }
    Evented<(ICharacter? speaker, SpeakFlags flags)> Speaker { get; }
    void ClearSpeaker();
    public VNOperation Say(LString content, ICharacter? character = default, SpeakFlags flags = SpeakFlags.Default);
}

public class DialogueBox : Rendered, IDialogueBox, IConfirmationReceiver {
    /// <summary>
    /// A dialogue request starts with a DialogueStarted proc containing the readable string of all text to be printed.
    /// It then unrolls the text one character at a time over many Dialogue procs.
    /// Finally, it procs DialogueFinished.
    /// Note: DialogueStarted and Dialogue are accumulated until a clear command is issued (which also procs DialogueCleared).
    /// </summary>
    public AccEvent<Speech> DialogueStarted { get; } = new();
    public AccEvent<(SpeechFragment frag, string lookahead)> Dialogue { get; } = new();
    public Event<Unit> DialogueFinished { get; } = new();
    public Event<Unit> DialogueCleared { get; } = new();
    public Evented<(ICharacter? speaker, SpeakFlags flags)> Speaker { get; } = new((default, SpeakFlags.Default));

    public void Clear() {
        Dialogue.Clear();
        DialogueStarted.Clear();
        DialogueCleared.OnNext(Unit.Default);
    }

    public void ClearSpeaker() => Speaker.OnNext((null, SpeakFlags.Default));

    private static bool LookaheadRequired(char c) => !char.IsWhiteSpace(c) && !char.IsPunctuation(c);
    private IEnumerator Say(Speech s, SpeakFlags flags, Action done, ICancellee cT) {
        if (cT.IsHardCancelled()) {
            done();
            yield break;
        }
        if ((flags & SpeakFlags.DontClearText) == 0) {
            Clear();
        }
        DialogueStarted.OnNext(s);
        float untilProceed = 0f;
        var lookahead = new StringBuilder();
        for (int ii = 0; ii < s.Fragments.Count; ++ii) {
            if (cT.IsHardCancelled()) break;
            var f = s.Fragments[ii];
            if (     f is SpeechFragment.Wait w)
                untilProceed += w.time;
            else if (f is SpeechFragment.RollEvent r) {
                if (!cT.Cancelled)
                    r.ev();
            } else {
                //determine lookahead only if we have a fragment to publish
                lookahead.Clear();
                for (int jj = ii; jj < s.Fragments.Count; ++jj) {
                    if (s.Fragments[jj] is SpeechFragment.Char c) {
                        if (LookaheadRequired(c.fragment)) {
                            if (jj != ii) lookahead.Append(c.fragment);
                        } else
                            break;
                    }
                }
                Dialogue.OnNext((f, lookahead.ToString()));
            }
            if (ii == s.Fragments.Count - 1)
                break;
            for (;untilProceed > 0; untilProceed -= Container.dT) {
                if (cT.Cancelled) 
                    break;
                yield return null;
            }
        }
        DialogueFinished.OnNext(Unit.Default);
        done();
    }

    public VNOperation Say(LString content, ICharacter? character = default, SpeakFlags flags = SpeakFlags.Default) =>
        this.MakeVNOp(cT => {
            Speaker.OnNext((character, flags));
            Run(Say(new Speech(content, character?.SpeechCfg), flags, WaitingUtils.GetAwaiter(out Task t), this.BindLifetime(cT)));
            return t;
        });
}
}