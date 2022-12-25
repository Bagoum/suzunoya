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
using JetBrains.Annotations;
using Suzunoya.ControlFlow;
using Suzunoya.Dialogue;

namespace Suzunoya.Entities {
/// <summary>
/// Flags describing features for dialogue execution.
/// </summary>
[Flags]
public enum SpeakFlags {
    /// <summary>
    /// No special features required.
    /// </summary>
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
    
    /// <summary>
    /// Default value- <see cref="None"/>
    /// </summary>
    Default = None,
}

/// <summary>
/// Interface for dialogue boxes.
/// </summary>
[PublicAPI]
public interface IDialogueBox : IEntity {
    /// <summary>
    /// A dialogue request starts with a DialogueStarted proc containing all information of the dialogue request.
    /// It then unrolls the text one character at a time into <see cref="Dialogue"/>.
    /// Finally, it procs <see cref="DialogueFinished"/>.
    /// <br/>Note: the VNContainer should be listening to this event to accumulate the dialogue log.
    /// </summary>
    AccEvent<DialogueOp> DialogueStarted { get; }
    
    /// <summary>
    /// All text fragments currently visible in the dialogue box. Cleared when <see cref="Clear"/> is called.
    /// </summary>
    AccEvent<(SpeechFragment frag, string lookahead)> Dialogue { get; }
    
    /// <summary>
    /// Event procced when a line of dialogue is finished unrolling.
    /// </summary>
    Event<Unit> DialogueFinished { get; }
    
    /// <summary>
    /// Event procced when <see cref="Clear"/> is called.
    /// </summary>
    Event<Unit> DialogueCleared { get; }
    
    /// <summary>
    /// The current character speaking in the dialogue box.
    /// </summary>
    Evented<(ICharacter? speaker, SpeakFlags flags)> Speaker { get; }
    
    /// <summary>
    /// Clear the dialogue box.
    /// </summary>
    void Clear(SpeakFlags? speakerClear = null);
    
    /// <summary>
    /// Have a character speak into the dialogue box.
    /// </summary>
    public VNOperation Say(LString content, ICharacter? character = default, SpeakFlags flags = SpeakFlags.Default);
}

/// <summary>
/// A line of dialogue to be printed into the dialogue box.
/// </summary>
public class DialogueOp {
    /// <summary>
    /// Dialogue text.
    /// </summary>
    public Speech Line { get; }
    /// <summary>
    /// The character that is speaking.
    /// </summary>
    public ICharacter? Speaker { get; }
    
    /// <inheritdoc cref="SpeakFlags"/>
    public SpeakFlags Flags { get; }
    
    /// <summary>
    /// The <see cref="VNLocation"/> produced by the dialogue operation.
    /// </summary>
    public VNLocation? Location { get; }
    
    /// <summary>
    /// Create a new <see cref="DialogueOp"/>.
    /// </summary>
    public DialogueOp(LString line, ICharacter? speaker, SpeakFlags flags, VNLocation? loc) {
        this.Line = new Speech(line, speaker?.Container.GlobalData.Settings, speaker?.SpeechCfg);
        this.Speaker = speaker;
        this.Flags = flags;
        this.Location = loc;
    }
    
    /// <inheritdoc/>
    public override string ToString() => $"{Speaker?.Name ?? "???"}:{Line.Readable}";
}

/// <summary>
/// An entity representing a dialogue box.
/// </summary>
[PublicAPI]
public class DialogueBox : Rendered, IDialogueBox, IConfirmationReceiver {
    /// <inheritdoc/>
    public AccEvent<DialogueOp> DialogueStarted { get; } = new();
    /// <inheritdoc/>
    public AccEvent<(SpeechFragment frag, string lookahead)> Dialogue { get; } = new();
    /// <inheritdoc/>
    public Event<Unit> DialogueFinished { get; } = new();
    /// <inheritdoc/>
    public Event<Unit> DialogueCleared { get; } = new();
    /// <inheritdoc/>
    public Evented<(ICharacter? speaker, SpeakFlags flags)> Speaker { get; } = new((default, SpeakFlags.Default));

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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