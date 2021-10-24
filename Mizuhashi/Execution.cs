namespace Mizuhashi {
public static class Execution {
    public static ParseResult<R, S> Run<R, S>(this Parser<R, S> p, InputStream<S> content) => p(content);
}
}