using System;
using System.Collections;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using BagoumLib;
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
    /// By default, every text command will reset all visible text and the speaker. To avoid this,
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
    AccEvent<DialogueOp> DialogueStarted { get; }
    AccEvent<(SpeechFragment frag, string lookahead)> Dialogue { get; }
    Event<Unit> DialogueCleared { get; }
    Evented<(ICharacter? speaker, SpeakFlags flags)> Speaker { get; }
    void Clear(SpeakFlags? speakerClear = null);
    public VNOperation Say(LString content, ICharacter? character = default, SpeakFlags flags = SpeakFlags.Default);
}

public class DialogueOp {
    public Speech Line { get; }
    public ICharacter? Speaker { get; }
    public SpeakFlags Flags { get; }
    public VNLocation? Location { get; }
    public DialogueOp(LString line, ICharacter? speaker, SpeakFlags flags, VNLocation? loc) {
        this.Line = new Speech(line, speaker?.Container.GlobalData.Settings, speaker?.SpeechCfg);
        this.Speaker = speaker;
        this.Flags = flags;
        this.Location = loc;
    }

    public override string ToString() => $"{Speaker?.Name ?? "???"}:{Line.Readable}";
}
public class DialogueBox : Rendered, IDialogueBox, IConfirmationReceiver {
    /// <summary>
    /// A dialogue request starts with a DialogueStarted proc containing all information of the dialogue request.
    /// It then unrolls the text one character at a time over many Dialogue procs.
    /// Finally, it procs DialogueFinished.
    /// Note: DialogueStarted and Dialogue are accumulated until a clear command is issued (which also procs DialogueCleared and nullifies Speaker). AllDialogue is never cleared.
    /// Note: the VNContainer should be listening to DialogueStarted to accumulate the dialogue log.
    /// </summary>
    public AccEvent<DialogueOp> DialogueStarted { get; } = new();
    public AccEvent<(SpeechFragment frag, string lookahead)> Dialogue { get; } = new();
    public Event<Unit> DialogueFinished { get; } = new();
    public Event<Unit> DialogueCleared { get; } = new();
    public Evented<(ICharacter? speaker, SpeakFlags flags)> Speaker { get; } = new((default, SpeakFlags.Default));

    public void Clear(SpeakFlags? speakerClear = null) {
        Dialogue.Clear();
        DialogueStarted.Clear();
        if (speakerClear.Try(out var s))
            Speaker.OnNext((null, s));
        DialogueCleared.OnNext(Unit.Default);
    }

    private static bool LookaheadRequired(char c) => !char.IsWhiteSpace(c);
    private IEnumerator Say(DialogueOp d, Action done, ICancellee cT) {
        if (cT.IsHardCancelled()) {
            done();
            yield break;
        }
        DialogueStarted.OnNext(d);
        float untilProceed = 0f;
        var lookahead = new StringBuilder();
        for (int ii = 0; ii < d.Line.Fragments.Count; ++ii) {
            if (cT.IsHardCancelled()) break;
            var f = d.Line.Fragments[ii];
            if (     f is SpeechFragment.Wait w)
                untilProceed += w.time;
            else if (f is SpeechFragment.RollEvent r) {
                if (!cT.Cancelled)
                    r.ev();
            } else {
                //determine lookahead only if we have a fragment to publish
                lookahead.Clear();
                for (int jj = ii; jj < d.Line.Fragments.Count; ++jj) {
                    if (d.Line.Fragments[jj] is SpeechFragment.Char c) {
                        if (LookaheadRequired(c.fragment)) {
                            if (jj != ii) lookahead.Append(c.fragment);
                        } else
                            break;
                    }
                }
                Dialogue.OnNext((f, lookahead.ToString()));
            }
            if (ii == d.Line.Fragments.Count - 1)
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
            Container.OperationID.OnNext(content.ID ?? content.defaultValue);
            if (!flags.HasFlag(SpeakFlags.DontClearText))
                Clear(null);
            Speaker.OnNext((character, flags));
            Run(Say(
                new DialogueOp(content, character, flags, VNLocation.Make(cT.vn)), 
                                WaitingUtils.GetAwaiter(out Task t), this.BindLifetime(cT)));
            return t;
        });
}
}