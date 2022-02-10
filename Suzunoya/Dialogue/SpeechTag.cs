namespace Suzunoya.Dialogue {

public abstract record SpeechTag {
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
    public record Unknown(string name, string? content) : SpeechTag { }
    
    /// <summary>
    /// Changes the speed of text unrolling.
    /// </summary>
    public record Speed(float multiplier) : SpeechTag {
        public override SpeechSettings ModifySettings(SpeechSettings src) =>
            src with {opsPerSecond = src.opsPerSecond * multiplier};

        public override bool RequiresRender => false;
    }

    /// <summary>
    /// Disables rolling events.
    /// </summary>
    public record Silent : SpeechTag {
        public override SpeechSettings ModifySettings(SpeechSettings src) =>
            src with {rollEventAllowed = (_, __) => false};

        public override bool RequiresRender => false;

        public override string ToString() => "Silent";
    }

    /// <summary>
    /// Changes text color.
    /// </summary>
    public record Color(string color) : SpeechTag {
        public override string ToString() => $"Color {{ color = {color} }}";
    }

    /// <summary>
    /// Shows furigana (ruby) next to the main text.
    /// </summary>
    public record Furigana(string furigana) : SpeechTag;
}
}