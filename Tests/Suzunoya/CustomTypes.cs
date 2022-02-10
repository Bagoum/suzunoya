using System;
using System.Numerics;
using BagoumLib.Events;
using Suzunoya;
using Suzunoya.Entities;
using Suzunoya.Dialogue;

namespace Tests.Suzunoya {
//Game-specific character information can be encoded in a common subclass which is used as the type param for IVNState<C>
public class MyTestCharacter : Character {
    public SpeechSettings speechCfg = new SpeechSettings(1, (s, i) => 1, 2, (s, i) => char.IsUpper(s[i]), null);
    public override SpeechSettings SpeechCfg => speechCfg;

    public new enum Emote {
        Happy,
        Angry,
        Neutral,
    }
    public Evented<Emote> Emotion { get; set; } = new(Emote.Neutral);
    public void SetEmote(Emote e) => Emotion.OnNext(e);
}
//Character-specific subclasses are for convenience, and also can contain character-specific information
public class Reimu : MyTestCharacter {
    public Evented<float> GoheiLength { get; } = new(14);
    public override string Name => "Reimu";
}

public class Yukari : MyTestCharacter {
    public Evented<Vector2> PositionOfYukarisChair { get; } = new(new Vector2(2f, 3f));
}

public class TestDialogueBox : DialogueBox {
    
}

}