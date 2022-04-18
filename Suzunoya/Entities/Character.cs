using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Suzunoya.ControlFlow;
using Suzunoya.Dialogue;

namespace Suzunoya.Entities {
public interface ICharacter : IRendered {
    IdealOverride<string?> Emote { get; }
    SpeechSettings SpeechCfg { get; }
    LString Name { get; }
}

public abstract class Character : Rendered, ICharacter {
    public virtual SpeechSettings SpeechCfg { get; } = SpeechSettings.Default;
    public virtual LString Name { get; set; } = "Nobody";

    public IdealOverride<string?> Emote { get; } = new(null);

    public VNOperation Say(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).Say(content, this, flags);

    public VNConfirmTask SayC(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        Say(content, box, flags).C;
    
    public VNOperation AlsoSay(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).AlsoSay(content, this, flags);

    public VNOperation AlsoSayN(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).AlsoSayN(content, this, flags);
}

}