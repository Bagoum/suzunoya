namespace Mizuhashi {
public static partial class Combinators {

    /// <inheritdoc cref="Between{T,A,B,C}(Parser{T,A},Parser{T,B},Parser{T,C})"/>
    public static Parser<char, B> Between<B>(char left, Parser<char, B> middle, char right) =>
        Between(Char(left), middle, Char(right));
    
    /// <summary>
    /// <see cref="Between{B}(char,Mizuhashi.Parser{char,B},char)"/>(outer, middle, outer)
    /// </summary>
    public static Parser<char, B> Between<B>(char outer, Parser<char, B> middle) =>
        Between(Char(outer), middle);
    
    /// <inheritdoc cref="Between{T,A,B,C}(Parser{T,A},Parser{T,B},Parser{T,C})"/>
    public static Parser<char, B> Between<B>(string left, Parser<char, B> middle, string right) =>
        Between(String(left), middle, String(right));
    
    /// <summary>
    /// <see cref="Between{B}(string,Mizuhashi.Parser{char,B},string)"/>(outer, middle, outer)
    /// </summary>
    public static Parser<char, B> Between<B>(string outer, Parser<char, B> middle) =>
        Between(String(outer), middle);

    /// <summary>
    /// Parse the given parser, then parse EOF.
    /// </summary>
    public static Parser<char, R> ThenEOF<R>(this Parser<char, R> p) => p.ThenIg(EOF<char>());
}

}