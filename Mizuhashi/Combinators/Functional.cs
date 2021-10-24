using System;

namespace Mizuhashi {
public static partial class Combinators {
    /// <summary>
    /// Parses an object of type A and then map it to type B.
    /// <br/>Equivalent to FParsec |>>
    /// </summary>
    public static Parser<B, S> FMap<A, B, S>(this Parser<A, S> p, Func<A, B> f) => input =>
        p(input).FMap(f);

    /// <summary>
    /// Parses a function of type A->B, then an argment of type A, and returns type B.
    /// </summary>
    public static Parser<B, S> Apply<A, B, S>(this Parser<Func<A, B>, S> p, Parser<A, S> arg) => input =>
        ParseResult<A, S>.Apply(p(input), arg);

    /// <summary>
    /// Parses an object of type A, then parses an object of type B depending on the value of A,
    ///  then returns the object of type B.
    /// <br/>Equivalent to FParsec >>=
    /// </summary>
    public static Parser<B, S> Bind<A, B, S>(this Parser<A, S> p, Func<A, Parser<B, S>> f) => input =>
        p(input).Bind(f);
    
    
    /// <summary>
    /// Parses an object of type A, then parses an object of type B depending on the value of A,
    ///  then finally combines the objects according to the project function.
    /// </summary>
    public static Parser<R, S> SelectMany<A, B, R, S>(this Parser<A, S> p, Func<A, Parser<B, S>> f, Func<A, B, R> project)
        => input => p(input).Bind(f, project);
    
    /// <summary>
    /// Parses an object of type A, then parses an object of type B,
    ///  then finally combines the objects according to the project function.
    /// <br/>Equivalent to FParsec pipe2. For more general pipes, use Sequential (not as optimized).
    /// </summary>
    public static Parser<R, S> Pipe<A, B, R, S>(this Parser<A, S> p, Parser<B, S> f, Func<A, B, R> project)
        => input => p(input).Bind(f, project);

    /// <summary>
    /// Allocation-efficient syntax for SelectMany when parsers do not depend on the return value of previous parsers.
    /// </summary>
    public static Parser<T, S> Sequential<T1, T2, T, S>(Parser<T1, S> p1, Parser<T2, S> p2, Func<T1, T2, T> map) =>
        from x1 in p1
        from x2 in p2
        select map(x1, x2);
    
    public static Parser<T, S> Sequential<T1, T2, T3, T, S>(Parser<T1, S> p1, Parser<T2, S> p2, Parser<T3, S> p3, Func<T1, T2, T3, T> map) =>
        from x1 in p1
        from x2 in p2
        from x3 in p3
        select map(x1, x2, x3);
    
    public static Parser<T, S> Sequential<T1, T2, T3, T4, T, S>(Parser<T1, S> p1, Parser<T2, S> p2, Parser<T3, S> p3, Parser<T4, S> p4, Func<T1, T2, T3, T4, T> map) =>
        from x1 in p1
        from x2 in p2
        from x3 in p3
        from x4 in p4
        select map(x1, x2, x3, x4);

    public static Parser<T, S> Sequential<T1, T2, T3, T4, T5, T, S>(Parser<T1, S> p1, Parser<T2, S> p2, Parser<T3, S> p3, Parser<T4, S> p4, Parser<T5, S> p5, Func<T1, T2, T3, T4, T5, T> map) =>
        from x1 in p1
        from x2 in p2
        from x3 in p3
        from x4 in p4
        from x5 in p5
        select map(x1, x2, x3, x4, x5);
    

}
}