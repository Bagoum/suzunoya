using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Events;
using Suzunoya.ControlFlow;
using Suzunoya.Dialogue;

namespace Suzunoya.Entities {
public interface ICharacter : ITransform, IEntity {
    SpeechSettings SpeechCfg { get; }
}

public abstract class Character : Rendered, ICharacter {
    public virtual SpeechSettings SpeechCfg { get; } = SpeechSettings.Default;

    public Character(bool visible = false, Vector3? location = null, Vector3? eulerAnglesD = null,
        Vector3? scale = null) :
        base(location, eulerAnglesD, scale, visible) {
        
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