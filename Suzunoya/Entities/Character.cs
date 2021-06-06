using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Suzunoya.ControlFlow;
using Suzunoya.Dialogue;

namespace Suzunoya.Entities {
public interface ICharacter : ITransform, IEntity {
    SpeechSettings SpeechCfg { get; }
    string Name { get; }
}

public abstract class Character : Rendered, ICharacter {
    public virtual SpeechSettings SpeechCfg { get; } = SpeechSettings.Default;
    public virtual string Name => "Nobody";

    public Evented<string?> Emote = new(null);

    public VNOperation Say(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).Say(content, this, flags);

    public VNConfirmTask SayC(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        Say(content, box, flags).C;
    
    public VNOperation AlsoSay(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).AlsoSay(content, this, flags);

    public VNOperation AlsoSayN(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).AlsoSayN(content, this, flags);

    public void Show() => Visible.Value = true;
}

}