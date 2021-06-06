namespace Suzunoya.Dialogue {

public abstract class SpeechTag {
    /// <summary>
    /// Apply modifications to the text unrolling settings.
    /// </summary>
    public virtual SpeechSettings ModifySettings(SpeechSettings src) => src;
    /// <summary>
    /// If FALSE, the tag is used internally for data management and does not need to be handled by rendering plugins.
    /// </summary>
    public virtual bool RequiresRender => true;

    /// <summary>
    /// A speech tag not handled by Suzunoya.
    /// </summary>
    public class Unknown : SpeechTag {
        public readonly string name;
        public readonly string? content;
        public Unknown(string name, string? content) {
            this.name = name;
            this.content = content;
        }
    }
    
    /// <summary>
    /// Changes the speed of text unrolling.
    /// </summary>
    public class Speed: SpeechTag {
        public readonly float multiplier;
        public Speed(float multiplier) {
            this.multiplier = multiplier;
        }

        public override SpeechSettings ModifySettings(SpeechSettings src) =>
            src with {opsPerSecond = src.opsPerSecond * multiplier};

        public override bool RequiresRender => false;
    }

    /// <summary>
    /// Disables rolling events.
    /// </summary>
    public class Silent : SpeechTag {
        public override SpeechSettings ModifySettings(SpeechSettings src) =>
            src with {rollEventAllowed = (_, __) => false};

        public override bool RequiresRender => false;

        public override string ToString() => "Silent";
    }

    /// <summary>
    /// Changes text color.
    /// </summary>
    public class Color : SpeechTag {
        public readonly string color;
        public Color(string color) {
            this.color = color;
        }

        public override string ToString() => $"Color {{ color = {color} }}";
    }

    /// <summary>
    /// Shows furigana (ruby) next to the main text.
    /// </summary>
    public class Furigana : SpeechTag {
        public readonly string furigana;
        public Furigana(string furigana) {
            this.furigana = furigana;
        }
    }
}
}