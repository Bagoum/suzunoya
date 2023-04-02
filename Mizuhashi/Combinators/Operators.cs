using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using static Mizuhashi.Combinators;

namespace Mizuhashi {

/// <summary>
/// The associativity of an infix operator.
/// </summary>
public enum Associativity {
    /// <summary>
    /// Left-associative, ie. x~y~z = (x~y)~z
    /// </summary>
    Left,
    /// <summary>
    /// Right-associative, ie. x~y~z = x~(y~z)
    /// </summary>
    Right,
    /// <summary>
    /// No associativity, ie. x~y~z throws an exception
    /// </summary>
    None
}

/// <summary>
/// Error shown when operator parsing fails due to ambiguous associativity.
/// </summary>
public record AmbiguousAssociativity<T, A, C>(ParseResult<(C, Operator<T, A, C>.Infix)> CurrentOp, ParseResult<(C, Operator<T, A, C>.Infix)> UnexpectedOp) : ParserError { 
    /// <inheritdoc/>
    public override string Show(IInputStream s, int streamIndex) {
        var w = s.TokenWitness;
        var co = CurrentOp.Result.Value;
        var uo = UnexpectedOp.Result.Value;
        if (co.Item2.Assoc == Associativity.None &&  uo.Item2.Assoc == Associativity.None)
            return 
                $"Found multiple non-associative operators of the same priority: {w.ShowConsumed(CurrentOp)} " +
                $"({w.ToPosition(CurrentOp)}) and {w.ShowConsumed(UnexpectedOp)} ({w.ToPosition(UnexpectedOp)})";
        return
            $"Found ambiguous {uo.Item2.Assoc.Show()}-associative operator " +
            $"{w.ShowConsumed(UnexpectedOp)} when parsing the {co.Item2.Assoc.Show()}-associative operator " +
            $"{w.ShowConsumed(CurrentOp)} ({w.ToPosition(CurrentOp)})";
    }
}

/// <summary>
/// Information about a parseable operator.
/// </summary>
/// <param name="Precedence">The precedence of the operator (higher precedences are parsed first).</param>
/// <typeparam name="A">The type that the operator manipulates. An operator is either unary, in which case it has type A -> A, or binary, in which case it has type A -> A -> A.</typeparam>
/// <typeparam name="T">Type of input stream token.</typeparam>
/// <typeparam name="C">Type of result of operator parser.</typeparam>
public record Operator<T, A, C>(int Precedence) {
    /// <summary>
    /// A prefix operator.
    /// </summary>
    public record Prefix : Operator<T, A, C> {
        /// <summary>
        /// Function that joins the operator and its operand(s) into a single entity.
        /// </summary>
        public Func<C, A, A> Op { get; }
        /// <summary>
        /// Parser that parses the operator.
        /// </summary>
        public Parser<T, (C, Prefix)> Parser { get; init; }
        /// <inheritdoc cref="Operator{T,A,C}.Prefix"/>
        public Prefix(Parser<T, C> rawOpParser, Func<C, A, A> op, int Precedence) : base(Precedence) {
            this.Op = op;
            this.Parser = rawOpParser.Then(PReturn<T, Prefix>(this));
        }

        /// <summary>
        /// Default parser that just returns the term itself.
        /// </summary>
        public static readonly Prefix Null = new(PReturn<T, C>(default!), (_, x) => x, -1);
    }

    /// <summary>
    /// A postfix operator.
    /// </summary>
    public record Postfix : Operator<T, A, C> {
        /// <inheritdoc cref="Operator{T,A,C}.Prefix.Op"/>
        public Func<A, C, A> Op { get; }
        /// <inheritdoc cref="Operator{T,A,C}.Prefix.Parser"/>
        public Parser<T, (C, Postfix)> Parser { get; init; }
        /// <inheritdoc cref="Operator{T,A,C}.Postfix"/>
        public Postfix(Parser<T, C> rawOpParser, Func<A, C, A> op, int Precedence) : base(Precedence) {
            this.Op = op;
            this.Parser = rawOpParser.Then(PReturn<T, Postfix>(this));
        }

        /// <summary>
        /// Default parser that just returns the term itself.
        /// </summary>
        public static readonly Postfix Null = new(PReturn<T, C>(default!), (x, _) => x, -1);
    }

    /// <summary>
    /// An infix operator.
    /// <br/>To help make clearer error messages, this takes the operator parser
    ///  and the resulting function as separate arguments.
    /// </summary>
    public record Infix : Operator<T, A, C> {
        /// <inheritdoc cref="Operator{T,A,C}.Prefix.Op"/>
        public Func<A, C, A, A> Op { get; }
        /// <inheritdoc cref="Associativity"/>
        public Associativity Assoc { get; }
        /// <inheritdoc cref="Operator{T,A,C}.Prefix.Parser"/>
        public Parser<T, (C, Infix)> Parser { get; }
        /// <inheritdoc cref="Operator{T,A,C}.Infix"/>
        
        public Infix(Parser<T, C> RawOperatorParser, Func<A, C, A, A> Op, Associativity Assoc, int Precedence) : base(Precedence) {
            this.Op = Op;
            this.Assoc = Assoc;
            Parser = RawOperatorParser.Then(PReturn<T, Infix>(this));
        }
    }
}


public static partial class Combinators {
    private static A Op<T, A, C>(this (C, Operator<T, A, C>.Infix) op, A left, A right) =>
        op.Item2.Op(left, op.Item1, right);
    
    /// <summary>
    /// Helper function to convert <see cref="Associativity"/> to 'left', 'right', or 'non'.
    /// </summary>
    public static string Show(this Associativity assoc) => assoc switch {
        Associativity.Left => "left",
        Associativity.Right => "right",
        Associativity.None => "non",
        _ => throw new ArgumentOutOfRangeException(nameof(assoc), assoc, null)
    };
    
    /// <summary>
    /// Parse a set of operators according to precedence and associativity rules.
    /// </summary>
    public static Parser<T, A> ParseOperators<T, A, C>(IEnumerable<Operator<T, A, C>> operators, Parser<T, A> term) {
        var table = operators.GroupBy(o => o.Precedence)
            .OrderByDescending(gr => gr.Key)
            .ToArray();
        //Each row in table is a list of operators with the same precedence
        //We go through each row and parse it
        return table.Aggregate(term, ParseSamePrecedenceOperators);
    }

    private static Parser<T, A> ParseSamePrecedenceOperators<T, A, C>(Parser<T, A> term, IEnumerable<Operator<T, A, C>> ops) {
        PartitionOperators(ops, out var rassoc, out var lassoc, out var nassoc, out var prefix, out var postfix);
        var rassocP = rassoc.Count == 0 ? null : ChoiceL("", rassoc.Select(r => r.Parser).ToArray());
        var lassocP = lassoc.Count == 0 ? null : ChoiceL("", lassoc.Select(r => r.Parser).ToArray());
        var nassocP = nassoc.Count == 0 ? null : ChoiceL("", nassoc.Select(r => r.Parser).ToArray());
        if (prefix.Count >= 0 || postfix.Count >= 0) {
            //For prefix/postfix, it's always ok to not parse anything. For infix, a successful parse can 
            // sometimes indicate an ambiguity, so we need separate handling for the null case.
            var prefixPR = ChoiceL("", prefix.Append(Operator<T,A,C>.Prefix.Null).Select(r => r.Parser).ToArray());
            var postfixPR = ChoiceL("", postfix.Append(Operator<T,A,C>.Postfix.Null).Select(r => r.Parser).ToArray());
            if (prefix.Count > 0 && postfix.Count > 0)
                term = Sequential(prefixPR, term, postfixPR, 
                    (pref, t, post) => post.Item2.Op(pref.Item2.Op(pref.Item1, t), post.Item1));
            else if (prefix.Count > 0)
                term = Sequential(prefixPR, term, (pref, t) => pref.Item2.Op(pref.Item1, t));
            else if (postfix.Count > 0)
                term = Sequential(term, postfixPR, (t, post) => post.Item2.Op(t, post.Item1));
        }

        return inp => {
            var rterm1 = term(inp);
            if (!rterm1.Result.Try(out var term1))
                return rterm1;

            //Check for ambiguity when (attemptedOp) failed after parsing (firstTerm) (precedingOp) (precedingTerm).
            ParseResult<A>? VerifyAmbiguityAndLast(Associativity assoc, 
                in ParseResult<(C, Operator<T, A, C>.Infix)> precedingOp,
                in ParseResult<A> precedingTerm,
                in ParseResult<(C, Operator<T, A, C>.Infix)> attemptedOp) {
                if (attemptedOp is { Status: ResultStatus.FATAL } att)
                    return att.CastFailure<A>();
                return VerifyAmbiguity(assoc, in precedingOp, in precedingTerm);
            }
            ParseResult<A>? VerifyAmbiguity(Associativity assoc, in ParseResult<(C, Operator<T, A, C>.Infix)> precedingOp, in ParseResult<A> precedingTerm) {
                var ss = inp.Stative;
                if (assoc != Associativity.Left && lassocP != null) {
                    var rLeft = lassocP(inp);
                    if (rLeft.Result.Valid)
                        return rLeft
                            .AsError<A>(new AmbiguousAssociativity<T, A, C>(precedingOp, rLeft))
                            .WithPreceding(in precedingTerm);
                }
                inp.RollbackFast(in ss);
                if (assoc != Associativity.Right && rassocP != null) {
                    var rRight = rassocP(inp);
                    if (rRight.Result.Valid)
                        return rRight
                            .AsError<A>(new AmbiguousAssociativity<T, A, C>(precedingOp, rRight))
                            .WithPreceding(in precedingTerm);
                }
                inp.RollbackFast(in ss);
                if (nassocP != null) {
                    //non-associative operators can't be sequenced, so
                    // we have to check for them even under the non-associative parser
                    var rNone = nassocP(inp);
                    if (rNone.Result.Valid)
                        return rNone
                            .AsError<A>(new AmbiguousAssociativity<T, A, C>(precedingOp, rNone))
                            .WithPreceding(in precedingTerm);
                    inp.RollbackFast(in ss);
                }
                return null;
            }

            //Try to parse right associations first
            var ropAndTerm1 = rterm1.FMap<(C, Operator<T, A, C>.Infix)>(_ => default!);
            if (rassocP == null)
                goto lassoc_stage;
            var rop = rassocP(inp);
            ropAndTerm1 = rop.WithPreceding(in rterm1);
            if (rop.Result.Try(out var op)) {
                var rterm2 = term(inp).WithPreceding(in ropAndTerm1);
                if (!rterm2.Result.Try(out var term2))
                    //Operators must be nonempty, so this is always a fatal error
                    return rterm2.CastFailure<A>();
                //At this point, we might have more terms to parse.
                //With right-associativity, we can't simplify terms until all of them are parsed.
                var rop2 = rassocP(inp);
                if (!rop2.Result.Try(out var op2))
                    //If we don't have more terms, verify that there's no left/neutral ambiguity, then end
                    return VerifyAmbiguityAndLast(Associativity.Right, in rop, in rterm2, in rop2) 
                           ?? rterm2.WithResult(op.Op(term1, term2));
                rop2 = rop2.WithPreceding(in rterm2);
                //If we have more terms, shift to a stack representation for everything except the first operator.
                var stack = new Stack<(A left, (C, Operator<T, A, C>.Infix) op)>();
                stack.Push((term2, op2));
                while (true) {
                    //Parse the second term (required)
                    rterm2 = term(inp).WithPreceding(in rop2);
                    if (!rterm2.Result.Try(out term2))
                        return rterm2.CastFailure<A>();
                    //Try to parse another operator
                    rop = rop2;
                    rop2 = rassocP(inp);
                    if (!rop2.Result.Try(out op2))
                        //If we don't have more terms, end here
                        break;
                    rop2 = rop2.WithPreceding(in rterm2);
                    stack.Push((term2, op2));
                }
                if (VerifyAmbiguityAndLast(Associativity.Right, in rop, in rterm2, in rop2).Try(out var ambiguous))
                    return ambiguous;
                while (stack.TryPop(out var left)) 
                    term2 = left.op.Op(left.left, term2);
                return rterm2.WithResult(op.Op(term1, term2));
            }
            
            if (rop.Status == ResultStatus.FATAL)
                return rop.CastFailure<A>();
            
            lassoc_stage: ;
            //Then try to parse left associations
            if (lassocP == null)
                goto nassoc_stage;
            rop = lassocP(inp);
            ropAndTerm1 = rop.WithPreceding(in ropAndTerm1);
            if (rop.Result.Try(out op)) {
                var rterm2 = term(inp).WithPreceding(in ropAndTerm1);
                if (!rterm2.Result.Try(out var term2))
                    return rterm2.CastFailure<A>();
                
                term1 = op.Op(term1, term2);
                //At this point, we might have more terms to parse.
                //With left-associativity, we can simplify as we parse.
                var rop2 = lassocP(inp);
                if (!rop2.Result.Try(out op))
                    return VerifyAmbiguityAndLast(Associativity.Left, in rop, in rterm2, in rop2) 
                           ?? rterm2.WithResult(term1);
                rop2 = rop2.WithPreceding(in rterm2);
                //If we have more terms, loop and simplify in the loop.
                while (true) {
                    //Parse the second term (required)
                    rterm2 = term(inp).WithPreceding(in rop2);
                    if (!rterm2.Result.Try(out term2))
                        return rterm2.CastFailure<A>();
                    term1 = op.Op(term1, term2);
                    //Try to parse another operator
                    rop = rop2;
                    rop2 = lassocP(inp);
                    if (!rop2.Result.Try(out op))
                        //If we don't have more terms, end here
                        break;
                    rop2 = rop2.WithPreceding(in rterm2);
                }
                if (VerifyAmbiguityAndLast(Associativity.Right, in rop, in rterm2, in rop2).Try(out var ambiguous))
                    return ambiguous;
                return rterm2.WithResult(term1);
            }
            
            if (rop.Status == ResultStatus.FATAL)
                return rop.CastFailure<A>();
            
            nassoc_stage : ;
            //Then try to parse non-associative associations
            if (nassocP == null)
                goto end;
            rop = nassocP(inp);
            ropAndTerm1 = rop.WithPreceding(in ropAndTerm1);
            if (rop.Result.Try(out op)) {
                var rterm2 = term(inp).WithPreceding(in ropAndTerm1);
                if (!rterm2.Result.Try(out var term2))
                    return rterm2.CastFailure<A>();

                //VerifyAmbiguity will fail if there are multiple terms
                return VerifyAmbiguity(Associativity.None, in rop, in rterm2) ?? rterm2.WithResult(op.Op(term1, term2));
            }

            end: ;
            return rterm1;
        };
    }

    private static void PartitionOperators<T, A, C>(IEnumerable<Operator<T, A, C>> ops, out List<Operator<T, A, C>.Infix> rassoc,
        out List<Operator<T, A, C>.Infix> lassoc, out List<Operator<T, A, C>.Infix> nassoc, out List<Operator<T, A, C>.Prefix> prefix,
        out List<Operator<T, A, C>.Postfix> postfix) {
        rassoc = new();
        lassoc = new();
        nassoc = new();
        prefix = new();
        postfix = new();
        foreach (var op in ops) {
            if      (op is Operator<T, A, C>.Prefix pref)
                prefix.Add(pref);
            else if (op is Operator<T, A, C>.Postfix post)
                postfix.Add(post);
            else if (op is Operator<T, A, C>.Infix inf)
                (inf.Assoc switch {
                    Associativity.Right => rassoc,
                    Associativity.Left => lassoc,
                    Associativity.None => nassoc,
                    _ => throw new ArgumentOutOfRangeException()
                }).Add(inf);
            else
                throw new ArgumentOutOfRangeException();
        }
    }
}

}