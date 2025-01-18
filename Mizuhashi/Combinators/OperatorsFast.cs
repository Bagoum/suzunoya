using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib.Functional;

namespace Mizuhashi;

/// <summary>
/// Information about a simple infix operator.
/// </summary>
public record FInfix<T, A>(T Token, Func<A, T, A, A> Op, Associativity Assoc, int Precedence, Func<T, ParserError?> Verify);

/// <summary>
/// Information about a simple prefix operator.
/// </summary>
public record FPrefix<T, A>(T Token, Func<T, A, A> Op, Func<T, ParserError?> Verify);

/// <summary>
/// Information about a simple postfix operator.
/// </summary>
public record FPostfix<T, A>(T Token, Func<A, T, A> Op, Func<T, ParserError?> Verify);

/// <summary>
/// Error shown when simple operator parsing fails due to ambiguous associativity.
/// </summary>
public record FAmbiguous<T, A>((T t, int ti, FInfix<T, A> op) CurrentOp, (T t, int ti, FInfix<T, A> op) UnexpectedOp) : ParserError { 
    /// <inheritdoc/>
    public override string Show(IInputStream s, int start, int end) {
        var w = s.TokenWitness;
        var (_, ci, cop) = CurrentOp;
        var (_, ui, uop) = UnexpectedOp;
        if (cop.Assoc == Associativity.None && cop.Assoc == Associativity.None)
            return 
                $"Found multiple non-associative operators of the same priority: {w.ShowConsumed(ci,ci+1)} " +
                $"({w.ToPosition(ci,ci+1)}) and {w.ShowConsumed(ui,ui+1)} ({w.ToPosition(ui,ui+1)})";
        return
            $"Found ambiguous {uop.Assoc.Show()}-associative operator " +
            $"{w.ShowConsumed(ui,ui+1)} when parsing the {cop.Assoc.Show()}-associative operator " +
            $"{w.ShowConsumed(ci,ci+1)} ({w.ToPosition(ci,ci+1)})";
    }
}

public static partial class Combinators {
    /// <summary>
    /// Parse prefix and postfix operators without precedence logic.
    /// Faster than <see cref="ParseOperators{T,A,C}"/> but requires that all operators are single tokens.
    /// </summary>
    public static Parser<T, A> ParsePrefixPostfixFast<T, A>(Parser<T, A> term, IEqualityComparer<T> matcher,
        FPrefix<T, A>[] prefixes, FPostfix<T, A>[] postfixes) {
        var prefMatch = prefixes.Select(op => (op.Token, op)).ToArray();
        var postMatch = postfixes.Select(op => (op.Token, op)).ToArray();
        return inp => {
            var starti = inp.Index;
            var pref1t = inp.MaybeNext!;
            FPrefix<T, A>? prefix = null;
            List<(T, FPrefix<T, A>)>? prefixes = null;
            foreach (var pref in prefMatch) {
                if (matcher.Equals(pref.Token, pref1t)) {
                    //NB: first prefix failure returns a nonfatal, which is required since many 
                    // prefix operators are overloaded (eg. `5 + 2`; `+` should not throw a prefix verify error)
                    if (pref.op.Verify(pref1t) is { } err)
                        return new ParseResult<A>(err, starti);
                    prefix = pref.op;
                    inp.Step();
                    goto prefixes;
                }
            }
            goto term;
            prefixes : ;
            var prefnt = inp.MaybeNext!;
            foreach (var pref in prefMatch) {
                if (matcher.Equals(pref.Token, prefnt)) {
                    if (pref.op.Verify(prefnt) is { } err)
                        return new ParseResult<A>(new LocatedParserError(inp.Index, err), starti, inp.Index);
                    (prefixes ??= []).Add((prefnt, pref.op));
                    inp.Step();
                    goto prefixes;
                }
            }
            term: ;
            var rterm = term(inp);
            if (!rterm.Result.Try(out var t))
                return new ParseResult<A>(Maybe<A>.None, rterm.Error, starti, rterm.End);
            if (prefixes != null) {
                for (int ii = prefixes.Count - 1; ii >= 0; --ii)
                    t = prefixes[ii].Item2.Op(prefixes[ii].Item1, t);
            }
            if (prefix != null)
                t = prefix.Op(pref1t, t);
            while (true) {
                var postnt = inp.MaybeNext!;
                foreach (var post in postMatch) {
                    if (matcher.Equals(post.Token, postnt)) {
                        //postfix ops never result in a failure, just an optional error
                        if (post.op.Verify(postnt) is { } err) {
                            rterm = rterm.WithErrAtEnd(err);
                            break;
                        }
                        t = post.op.Op(t, postnt);
                        inp.Step();
                        goto nxt;
                    }
                }
                break;
                nxt: ;
            }
            return new ParseResult<A>(t, rterm.Error, starti, inp.Index);
        };
    }
    
    /// <summary>
    /// Parse infix operators according to precedence and associativity rules.
    ///  Faster than <see cref="ParseOperators{T,A,C}"/> but requires that all operators are single tokens.
    /// </summary>
    public static Parser<T, A> ParseOperatorsFast<T, A>(Parser<T, A> term, IEqualityComparer<T> matcher, params FInfix<T, A>[] operators) where T: notnull {
        var dct = operators.ToDictionary(x => x.Token, matcher);
        return inp => {
            var rt1 = term(inp);
            if (!rt1.Result.Try(out var t1) || inp.Empty)
                return rt1;
            var o1t = inp.Next;
            var o1ti = inp.Index;
            if (!dct.TryGetValue(o1t, out var op1))
                return rt1;
            if (op1.Verify(o1t) is { } err1)
                return rt1.WithErrAtEnd(err1);
            inp.Step();
            var rt2 = term(inp).WithPreceding(rt1);
            if (!rt2.Result.Try(out var t2))
                return rt2;
            var o2t = inp.MaybeNext!;
            var o2ti = inp.Index;
            //no ambiguity checks if we only have one operator :)
            if (!dct.TryGetValue(o2t, out var op2))
                return rt2.WithResult(op1.Op(t1, o1t, t2));
            if (op2.Verify(o2t) is { } err2)
                return rt2.WithResult(op1.Op(t1, o1t, t2)).WithErrAtEnd(err2);
            inp.Step();

            var terms = new List<A>() { t1, t2 };
            var ops = new List<(T token, int ti, FInfix<T, A> op)>() { (o1t, o1ti, op1), (o2t, o2ti, op2) };
            var precs = new HashSet<int>() { op1.Precedence, op2.Precedence };
            var rprevT = rt2;

            while (true) {
                if (terms.Count == ops.Count) {
                    //parse term (required)
                    var rnextT = term(inp).WithPreceding(rprevT);
                    if (!rnextT.Result.Try(out var nextT))
                        return rnextT;
                    rprevT = rnextT;
                    terms.Add(nextT);
                } else {
                    //try parse op or break
                    var ont = inp.MaybeNext!;
                    if (!dct.TryGetValue(ont, out var opn))
                        break;
                    if (opn.Verify(ont) is { } errn) {
                        rprevT = rprevT.WithErrAtEnd(errn);
                        break;
                    }
                    ops.Add((ont, inp.Index, opn));
                    precs.Add(opn.Precedence);
                    inp.Step();
                }
            }

            foreach (var p in precs.OrderByDescending(x => x)) {
                for (int oi = 0; oi < ops.Count; ++oi) {
                    if (ops[oi].op.Precedence != p) continue;
                    var assoc = ops[oi].op.Assoc;
                    var oj = oi + 1;
                    for (; oj < ops.Count; ++oj) {
                        if (ops[oj].op.Precedence != p) break;
                        if (ops[oj].op.Assoc != assoc || assoc is Associativity.None) //no consecutive None assocs!
                            return rprevT.AsError<A>(new FAmbiguous<T, A>(ops[oi], ops[oj]));
                    }
                    //operators [oi,oj) is a range of same-precedence operators we can reduce
                    if (assoc is Associativity.None or Associativity.Left) {
                        var val = terms[oi];
                        for (int k = oi; k < oj; ++k) {
                            var (t, _, op) = ops[k];
                            val = op.Op(val, t, terms[k + 1]);
                        }
                        terms[oi] = val;
                    } else {
                        var val = terms[oj];
                        for (int k = oj - 1; k >= oi; --k) {
                            var (t, _, op) = ops[k];
                            val = op.Op(terms[k], t, val);
                        }
                        terms[oi] = val;
                    }
                    terms.RemoveRange(oi + 1, oj - oi);
                    ops.RemoveRange(oi, oj - oi);
                }
            }
            if (ops.Count > 0 || terms.Count != 1)
                throw new Exception("Error in operator parser! Unhandled operations still exist!");
            return rprevT.WithResult(terms[0]);
        };
    }
}