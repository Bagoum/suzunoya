using System;
using System.Linq;
using BagoumLib.Functional;

namespace Mizuhashi {
/// <summary>
/// Parser combinator functions
/// </summary>
public static partial class Combinators {
    /// <summary>
    /// Parses an object of type A and then map it to type B.
    /// <br/>Equivalent to FParsec |>>
    /// </summary>
    public static Parser<T, B> FMap<T, A, B>(this Parser<T, A> p, Func<A, B> f) => input =>
        p(input).FMap(f);
    
    /// <summary>
    /// Alias for FMap for LINQ compatibility.
    /// </summary>
    public static Parser<T, B> Select<T, A, B>(this Parser<T, A> p, Func<A, B> f) => FMap(p, f);

    /// <summary>
    /// Parses a function of type A->B, then an argument of type A, and returns type B.
    /// </summary>
    public static Parser<T, B> Apply<T, A, B>(this Parser<T, Func<A, B>> p, Parser<T, A> arg) => input => {
        var rFunc = p(input);
        if (rFunc.Result.Try(out var func)) {
            var rArg = arg(input);
            return new(rArg.Result.FMap(func), rFunc.MergeErrors(in rArg), rFunc.Start, rArg.End);
        } else
            return rFunc.CastFailure<B>();
    };

    /// <summary>
    /// Parses an object of type A, then parses an object of type B depending on the value of A,
    ///  then returns the object of type B.
    /// <br/>Equivalent to FParsec >>=
    /// </summary>
    public static Parser<T, B> Bind<T, A, B>(this Parser<T, A> p, Func<A, Parser<T, B>> f) => input => {
        var rx = p(input);
        if (rx.Result.Try(out var x)) {
            var ry = f(x)(input);
            return new(ry.Result, rx.MergeErrors(in ry), rx.Start, ry.End);
        } else
            return rx.CastFailure<B>();
    };
    
    /// <summary>
    /// Parses an object of type A, then returns a result of type B depending on the value of A.
    /// </summary>
    public static Parser<T, B> Bind<T, A, B>(this Parser<T, A> p, Func<A, ParseResult<B>> b) => input => {
        var rx = p(input);
        if (rx.Result.Try(out var x)) {
            return b(x);
        } else
            return rx.CastFailure<B>();
    };
    
    
    /// <summary>
    /// Parses an object of type A, then parses an object of type B depending on the value of A,
    ///  then finally combines the objects according to the project function.
    /// </summary>
    public static Parser<T, R> Bind<T, A, B, R>(this Parser<T, A> p, Func<A, Parser<T, B>> f, Func<A, B, R> project)
        => input => {
            var rx = p(input);
            if (rx.Result.Try(out var x)) {
                var ry = f(x)(input);
                return new(ry.Result.Try(out var y) ? new(project(x, y)) : Maybe<R>.None, 
                    rx.MergeErrors(in ry), rx.Start, ry.End);
            } else
                return rx.CastFailure<R>();
        };

    /// <summary>
    /// Alias for Bind for LINQ compatibility.
    /// </summary>
    public static Parser<T, R> SelectMany<T, A, B, R>(this Parser<T, A> p, Func<A, Parser<T, B>> f, Func<A, B, R> project)
        => p.Bind(f, project);
    
    /// <summary>
    /// Parses an object of type A, then parses an object of type B,
    ///  then finally combines the objects using the project function.
    /// <br/>FParsec pipe2
    /// </summary>
    public static Parser<T, R> Pipe<T, A, B, R>(this Parser<T, A> p, Parser<T, B> p2, Func<A, B, R> project)
        => input => {
            var ra = p(input);
            if (!ra.Result.Try(out var a))
                return ra.CastFailure<R>();
            var rb = p2(input);
            return new(rb.Result.Try(out var b) ? 
                    new(project(a, b)) : 
                    Maybe<R>.None, 
                ra.MergeErrors(in rb), ra.Start, rb.End);
        };

    /// <summary>
    /// Sequentially parses objects of type A, B, C, then combines them using the project function.
    /// <br/>FParsec pipe3
    /// </summary>
    public static Parser<T, R> Pipe3<T, A, B, C, R>(this Parser<T, A> p, Parser<T, B> p2, Parser<T, C> p3, Func<A, B, C, R> project)
        => input => {
            var ra = p(input);
            if (!ra.Result.Try(out var a))
                return ra.CastFailure<R>();
            var rb = p2(input).WithPreceding(in ra);
            if (!rb.Result.Try(out var b))
                return rb.CastFailure<R>();
            var rc = p3(input);
            return new(rc.Result.Try(out var c) ?
                    new(project(a, b, c)) :
                    Maybe<R>.None,
                rb.MergeErrors(in rc), rb.Start, rc.End);
        };
    
    
    /// <summary>
    /// Sequentially parses objects of type A, B, C, D, then combines them using the project function.
    /// <br/>FParsec pipe4
    /// </summary>
    public static Parser<T, R> Pipe4<T, A, B, C, D, R>(this Parser<T, A> p, Parser<T, B> p2, Parser<T, C> p3, Parser<T, D> p4, Func<A, B, C, D, R> project)
        => input => {
            var ra = p(input);
            if (!ra.Result.Try(out var a))
                return ra.CastFailure<R>();
            var rb = p2(input).WithPreceding(in ra);
            if (!rb.Result.Try(out var b))
                return rb.CastFailure<R>();
            var rc = p3(input).WithPreceding(in rb);
            if (!rc.Result.Try(out var c))
                return rc.CastFailure<R>();
            var rd = p4(input);
            return new(rd.Result.Try(out var d) ?
                    new(project(a, b, c, d)) :
                    Maybe<R>.None,
                rc.MergeErrors(in rd), rc.Start, rd.End);
        };
    
    
    
    /// <summary>
    /// Sequentially parses objects of type A, B, C, D, E, then combines them using the project function.
    /// <br/>FParsec pipe5
    /// </summary>
    public static Parser<T, R> Pipe5<T, A, B, C, D, E, R>(this Parser<T, A> p, Parser<T, B> p2, Parser<T, C> p3, Parser<T, D> p4, Parser<T, E> p5, Func<A, B, C, D, E, R> project)
        => input => {
            var ra = p(input);
            if (!ra.Result.Try(out var a))
                return ra.CastFailure<R>();
            var rb = p2(input).WithPreceding(in ra);
            if (!rb.Result.Try(out var b))
                return rb.CastFailure<R>();
            var rc = p3(input).WithPreceding(in rb);
            if (!rc.Result.Try(out var c))
                return rc.CastFailure<R>();
            var rd = p4(input).WithPreceding(in rc);
            if (!rd.Result.Try(out var d))
                return rd.CastFailure<R>();
            var re = p5(input);
            return new(re.Result.Try(out var e) ?
                    new(project(a, b, c, d, e)) :
                    Maybe<R>.None,
                rd.MergeErrors(in re), rd.Start, re.End);
        };
    
    
    /// <summary>
    /// Sequentially parses objects of type A, B, C, D, E, F, then combines them using the project function.
    /// </summary>
    public static Parser<T, R> Pipe6<T, A, B, C, D, E, F, R>(this Parser<T, A> p, Parser<T, B> p2, Parser<T, C> p3, Parser<T, D> p4, Parser<T, E> p5, Parser<T, F> p6, Func<A, B, C, D, E, F, R> project)
        => input => {
            var ra = p(input);
            if (!ra.Result.Try(out var a))
                return ra.CastFailure<R>();
            var rb = p2(input).WithPreceding(in ra);
            if (!rb.Result.Try(out var b))
                return rb.CastFailure<R>();
            var rc = p3(input).WithPreceding(in rb);
            if (!rc.Result.Try(out var c))
                return rc.CastFailure<R>();
            var rd = p4(input).WithPreceding(in rc);
            if (!rd.Result.Try(out var d))
                return rd.CastFailure<R>();
            var re = p5(input).WithPreceding(in rd);
            if (!re.Result.Try(out var e))
                return re.CastFailure<R>();
            var rf = p6(input);
            return new(rf.Result.Try(out var f) ?
                    new(project(a, b, c, d, e, f)) :
                    Maybe<R>.None,
                re.MergeErrors(in rf), re.Start, rf.End);
        };

    /// <summary>
    /// Allocation-efficient syntax for SelectMany when parsers do not depend on the return value of previous parsers.
    /// <br/>Delegates to Pipe when possible, as it is more efficient than SelectMany.
    /// </summary>
    public static Parser<Token, T> Sequential<Token, T1, T2, T>(Parser<Token, T1> p1, Parser<Token, T2> p2, Func<T1, T2, T> map) =>
        Pipe(p1, p2, map);

    /// <inheritdoc cref="Sequential{Token,T1,T2,T}"/>
    public static Parser<Token, T> Sequential<Token, T1, T2, T3, T>(Parser<Token, T1> p1, Parser<Token, T2> p2, Parser<Token, T3> p3, Func<T1, T2, T3, T> map) =>
        Pipe3(p1, p2, p3, map);

    /// <inheritdoc cref="Sequential{Token,T1,T2,T}"/>
    public static Parser<Token, T> Sequential<Token, T1, T2, T3, T4, T>(Parser<Token, T1> p1, Parser<Token, T2> p2, Parser<Token, T3> p3, Parser<Token, T4> p4,
        Func<T1, T2, T3, T4, T> map) =>
        Pipe4(p1, p2, p3, p4, map);

    /// <inheritdoc cref="Sequential{Token,T1,T2,T}"/>
    public static Parser<Token, T> Sequential<Token, T1, T2, T3, T4, T5, T>(Parser<Token, T1> p1, Parser<Token, T2> p2, Parser<Token, T3> p3,
        Parser<Token, T4> p4, Parser<Token, T5> p5, Func<T1, T2, T3, T4, T5, T> map) =>
        Pipe5(p1, p2, p3, p4, p5, map);


    /// <inheritdoc cref="Sequential{Token,T1,T2,T}"/>
    public static Parser<Token, T> Sequential<Token, T1, T2, T3, T4, T5, T6, T>(Parser<Token, T1> p1, Parser<Token, T2> p2, Parser<Token, T3> p3,
        Parser<Token, T4> p4, Parser<Token, T5> p5, Parser<Token, T6> p6, Func<T1, T2, T3, T4, T5, T6, T> map) =>
        Pipe6(p1, p2, p3, p4, p5, p6, map);

}
}