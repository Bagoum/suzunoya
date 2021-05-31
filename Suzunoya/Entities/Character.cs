using System.Numerics;
using System.Threading.Tasks;
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

    public Character(bool visible = true, Vector3? location = null, Vector3? eulerAnglesD = null,
        Vector3? scale = null) :
        base(location, eulerAnglesD, scale, visible) {
        Tint.Value = new FColor(1, 1, 1, 0);
    }

    public VNOperation Say(string content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).Say(content, this, flags);
    public VNOperation AlsoSay(string content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).AlsoSay(content, this, flags);

    public VNOperation AlsoSayN(string content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).AlsoSayN(content, this, flags);

    public void Show() => Visible.Value = true;
}

}