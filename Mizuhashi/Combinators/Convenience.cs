namespace Mizuhashi {
public static partial class Combinators {

    public static Parser<B> Between<B>(char left, Parser<B> middle, char right) =>
        Between(Char(left), middle, Char(right));
    
    public static Parser<B> Between<B>(char outer, Parser<B> middle) =>
        Between(Char(outer), middle);
    
    public static Parser<B> Between<B>(string left, Parser<B> middle, string right) =>
        Between(String(left), middle, String(right));
    
    public static Parser<B> Between<B>(string outer, Parser<B> middle) =>
        Between(String(outer), middle);

    public static Parser<R> ThenEOF<R>(this Parser<R> p) => p.ThenIg(EOF);
}

}