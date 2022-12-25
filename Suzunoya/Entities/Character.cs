using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Suzunoya.ControlFlow;
using Suzunoya.Dialogue;

namespace Suzunoya.Entities {
/// <summary>
/// Interface for entities that are VN characters. Characters can speak in dialogue boxes.
/// </summary>
public interface ICharacter : IRendered {
    /// <summary>
    /// Emote assigned to the character.
    /// </summary>
    IdealOverride<string?> Emote { get; }
    
    /// <summary>
    /// Speech settings for the character.
    /// </summary>
    SpeechSettings SpeechCfg { get; }
    
    /// <summary>
    /// Name of the character.
    /// </summary>
    LString Name { get; }
}

/// A character entity.
public abstract class Character : Rendered, ICharacter {
    /// <inheritdoc/>
    public virtual SpeechSettings SpeechCfg { get; } = SpeechSettings.Default;
    /// <inheritdoc/>
    public virtual LString Name { get; set; } = "Nobody";

    /// <inheritdoc/>
    public IdealOverride<string?> Emote { get; } = new(null);

    /// <inheritdoc cref="IDialogueBox.Say"/>
    public VNOperation Say(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).Say(content, this, flags);

    /// <summary>
    /// Have the character speak into the dialogue box, then wait for user confirmation.
    /// </summary>
    public VNConfirmTask SayC(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        Say(content, box, flags).C;
    
    /// <summary>
    /// Have the character speak into the dialogue box without clearing the existing text.
    /// </summary>
    public VNOperation AlsoSay(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).AlsoSay(content, this, flags);

    /// <summary>
    /// Have the character speak into the dialogue box without clearing the existing text
    ///  but adding a newline before printing the new text.
    /// </summary>
    public VNOperation AlsoSayN(LString content, IDialogueBox? box = null, SpeakFlags flags = SpeakFlags.Default) =>
        (box ?? Container.MainDialogueOrThrow).AlsoSayN(content, this, flags);
}

}