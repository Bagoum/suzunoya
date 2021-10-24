namespace Mizuhashi {
public static partial class Combinators {

    public static Parser<B, S> Between<B, S>(char left, Parser<B, S> middle, char right) =>
        Between(Char<S>(left), middle, Char<S>(right));
    
    public static Parser<B, S> Between<B, S>(char outer, Parser<B, S> middle) =>
        Between(Char<S>(outer), middle);
    
    public static Parser<B, S> Between<B, S>(string left, Parser<B, S> middle, string right) =>
        Between(String<S>(left), middle, String<S>(right));
    
    public static Parser<B, S> Between<B, S>(string outer, Parser<B, S> middle) =>
        Between(String<S>(outer), middle);

    public static Parser<R, S> ThenEOF<R, S>(this Parser<R, S> p) => p.ThenIg(EOF<S>());
}

}