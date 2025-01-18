using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Functional;
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
    public override string Show(IInputStream s, int start, int end) {
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

    private enum OperatorParseDelegation {
        START = 0,
        DELEGATE_TERM1 = 1,
        DELEGATE_RTERM1 = 2,
        DELEGATE_RTERMSTACK = 3,
        DELEGATE_LTERM = 4,
        DELEGATE_NTERM = 5,
    }
    private struct OperatorParseFrame<T, A, C> {
        public readonly Parser<T,(C, Operator<T,A,C>.Infix)>? rassocP;
        public readonly Parser<T,(C, Operator<T,A,C>.Infix)>? lassocP;
        public readonly Parser<T,(C, Operator<T,A,C>.Infix)>? nassocP;
        public readonly Parser<T, (C, Operator<T, A, C>.Prefix)>? prefixP;
        public readonly Parser<T, (C, Operator<T, A, C>.Postfix)>? postfixP;
        public readonly int opInd;
        public OperatorParseDelegation stage;
        public Maybe<(C, Operator<T, A, C>.Prefix)> delTermPrefix;
        public A term1;
        public ParseResult<(C, Operator<T, A, C>.Infix)> prevOp;
        public Stack<((C, Operator<T, A, C>.Infix) op, A right)>? rtermAssoc;


        public OperatorParseFrame(int opInd, (Parser<T,(C, Operator<T,A,C>.Infix)>?, Parser<T,(C, Operator<T,A,C>.Infix)>?, Parser<T,(C, Operator<T,A,C>.Infix)>?, Parser<T,(C, Operator<T,A,C>.Prefix)>?, Parser<T,(C, Operator<T,A,C>.Postfix)>?)[] table) {
            this.opInd = opInd;
            if (opInd < 0) {
                (rassocP, lassocP, nassocP, prefixP, postfixP) = (null, null, null, null, null);
            } else {
                (rassocP, lassocP, nassocP, prefixP, postfixP) = table[opInd];
            }
            this.stage = OperatorParseDelegation.START;
            delTermPrefix = default;
            term1 = default!;
            prevOp = default;
            rtermAssoc = null;
        }
    }

    private static A UsePrefix<T, A, C>(A value, (C, Operator<T, A, C>.Prefix) prefix) {
        return prefix.Item2.Op(prefix.Item1, value);
    }
    private static A CombinePrefixPostfix<T, A, C>(A value, Maybe<(C, Operator<T, A, C>.Prefix)> prefix,
        (C, Operator<T, A, C>.Postfix) postfix) {
        if (prefix.Try(out var pr))
            value = pr.Item2.Op(pr.Item1, value);
        return postfix.Item2.Op(value, postfix.Item1);
    }

    //ERRORs are dropped in operator parsing and not reported
    // this is because operators are always optional when parsing greedily
    // as such, we don't need to add a specific error message the way that `choiceL` does,
    // and we don't need to combine errors the way that `choice` does
    private static Parser<T, R>? _OpChoice<T, R>(IEnumerable<Parser<T, R>> parsers) {
        var ps = parsers.ToArray();
        if (ps.Length == 0) return null;
        if (ps.Length == 1) return ps[0]; //this only works because we don't need special error handling
        return input => {
            ParseResult<R> result = default!;
            for (int ii = 0; ii < ps.Length; ++ii) {
                result = ps[ii](input);
                if (result.Status != ResultStatus.ERROR)
                    return result;
            }
            return result;
        };
    }

    /// <summary>
    /// Parse a set of operators according to precedence and associativity rules.
    /// </summary>
    public static Parser<T, A>
        ParseOperators<T, A, C>(IEnumerable<Operator<T, A, C>> operators, Parser<T, A> term) {
        var table = operators.GroupBy(o => o.Precedence)
            .OrderByDescending(gr => gr.Key)
            .Select(gr => {
                PartitionOperators(gr, out var rassoc, out var lassoc, out var nassoc, out var prefix, out var postfix);
                var rassocP = _OpChoice(rassoc.Select(r => r.Parser));
                var lassocP = _OpChoice(lassoc.Select(r => r.Parser));
                var nassocP = _OpChoice(nassoc.Select(r => r.Parser));
                var prefixP = _OpChoice(prefix.Select(r => r.Parser));
                var postfixP = _OpChoice(postfix.Select(r => r.Parser));
                return (rassocP, lassocP, nassocP, prefixP, postfixP);
            })
            .ToArray();
        return inp => {
            //Start at the outermost (lowest precedence) operators
            var evalStack = new Stack<OperatorParseFrame<T, A, C>>();
            evalStack.Push(new(table.Length - 1, table));
            ParseResult<Unit>? prev = null;
            A result = default!;
            ParseResult<R> JoinParsed<R>(ParseResult<R> result) {
                result = result.WithPrecedingNullable(prev);
                prev = result.Erase();
                return result;
            }
            //Check for ambiguity when (attemptedOp) failed after parsing (firstTerm) (precedingOp) (precedingTerm).
            ParseResult<A>? VerifyAmbiguityAndLast(OperatorParseFrame<T, A, C> frame, Associativity assoc,
                in ParseResult<(C, Operator<T, A, C>.Infix)> precedingOp,
                in ParseResult<(C, Operator<T, A, C>.Infix)> attemptedOp) {
                if (attemptedOp is { Status: ResultStatus.FATAL } att)
                    return att.CastFailure<A>();
                return VerifyAmbiguity(frame, assoc, in precedingOp);
            }
            ParseResult<A>? VerifyAmbiguity(OperatorParseFrame<T, A, C> f, Associativity assoc,
                in ParseResult<(C, Operator<T, A, C>.Infix)> precedingOp) {
                var ss = inp.Stative;
                if (assoc != Associativity.Left && f.lassocP != null) {
                    var rLeft = f.lassocP(inp);
                    if (rLeft.Result.Valid)
                        return rLeft
                            .AsError<A>(new AmbiguousAssociativity<T, A, C>(precedingOp, rLeft))
                            .WithPrecedingNullable(prev);
                }
                inp.RollbackFast(in ss);
                if (assoc != Associativity.Right && f.rassocP != null) {
                    var rRight = f.rassocP(inp);
                    if (rRight.Result.Valid)
                        return rRight
                            .AsError<A>(new AmbiguousAssociativity<T, A, C>(precedingOp, rRight))
                            .WithPrecedingNullable(prev);
                }
                inp.RollbackFast(in ss);
                if (f.nassocP != null) {
                    //non-associative operators can't be sequenced, so
                    // we have to check for them even under the non-associative parser
                    var rNone = f.nassocP(inp);
                    if (rNone.Result.Valid)
                        return rNone
                            .AsError<A>(new AmbiguousAssociativity<T, A, C>(precedingOp, rNone))
                            .WithPrecedingNullable(prev);
                    inp.RollbackFast(in ss);
                }
                return null;
            }

            while (evalStack.TryPop(out var f)) {
                switch (f.stage) {
                    case OperatorParseDelegation.START:
                        goto start;
                    case OperatorParseDelegation.DELEGATE_TERM1:
                        goto return_term1;
                    case OperatorParseDelegation.DELEGATE_RTERM1:
                        goto return_rterm1;
                    case OperatorParseDelegation.DELEGATE_RTERMSTACK:
                        goto return_rtermStack;
                    case OperatorParseDelegation.DELEGATE_LTERM:
                        goto return_lterm;
                    case OperatorParseDelegation.DELEGATE_NTERM:
                        goto return_nterm;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                ParseResult<A>? ParsePrefix() {
                    if (f.prefixP is not null) {
                        var rprefix = f.prefixP(inp);
                        if (rprefix.Status == ResultStatus.FATAL)
                            return rprefix.CastFailure<A>();
                        if (rprefix.Result.Valid)
                            JoinParsed(rprefix);
                        f.delTermPrefix = rprefix.Result;
                    } else
                        f.delTermPrefix = Maybe<(C, Operator<T, A, C>.Prefix)>.None;
                    return null;
                }
                ParseResult<A>? ParsePostfix(out A combined) {
                    if (f.postfixP is not null) {
                        var rpostfix = f.postfixP(inp);
                        if (rpostfix.Status == ResultStatus.FATAL) {
                            combined = default!;
                            return rpostfix.CastFailure<A>();
                        }
                        if (rpostfix.Result.Valid) {
                            JoinParsed(rpostfix);
                            combined = CombinePrefixPostfix(result, f.delTermPrefix, rpostfix.Result.Value);
                            return null;
                        }
                    }
                    combined = f.delTermPrefix.Valid ?
                        UsePrefix(result, f.delTermPrefix.Value) :
                        result;
                    return null;
                }
                start: ;
                if (f.opInd < 0) {
                    //Get basic term and return to previous frame
                    //Since a basic term is always required when it is requested, we can exit the function
                    // if this errors
                    var pr = JoinParsed(term(inp));
                    if (!pr.Result.Try(out result))
                        return pr;
                    continue;
                }
                //Parse first term into f.term1
                //First term prefix
                if (ParsePrefix() is { } prefixErr)
                    return prefixErr;

                //Delegate to higher priority operators for first term proper
                f.stage = OperatorParseDelegation.DELEGATE_TERM1;
                evalStack.Push(f);
                f = new(f.opInd - 1, table);
                goto start;
                return_term1: ;
                //First term postfix
                if (ParsePostfix(out f.term1) is { } postfixErr)
                    return postfixErr;

                //Parse right associations
                if (f.rassocP is null)
                    goto lassoc_stage;
                //First right-assoc operator
                f.prevOp = f.rassocP(inp);
                if (f.prevOp.Status == ResultStatus.FATAL)
                    return f.prevOp.CastFailure<A>();
                if (!f.prevOp.Result.Valid)
                    goto lassoc_stage;
                JoinParsed(f.prevOp);
                //Parse first right-assoc term into rTerm
                //First right-assoc term prefix
                if (ParsePrefix() is { } prefixErrR)
                    return prefixErrR;
                //Delegate to higher priority operators for term proper
                f.stage = OperatorParseDelegation.DELEGATE_RTERM1;
                evalStack.Push(f);
                f = new(f.opInd - 1, table);
                goto start;
                return_rterm1: ;
                //First right-assoc term postfix
                if (ParsePostfix(out var rTerm) is { } postfixErrR)
                    return postfixErrR;
                //Check if we have more terms to parse
                var rop2 = f.rassocP!(inp);
                if (!rop2.Result.Valid) {
                    //If we don't have more terms, verify that there's no left/neutral ambiguity, then end this loop
                    if (VerifyAmbiguityAndLast(f, Associativity.Right, in f.prevOp, in rop2) is { } err)
                        return err;
                    result = f.prevOp.Result.Value.Op(f.term1, rTerm);
                    continue;
                }
                JoinParsed(rop2);
                //If we do have more terms, shift to a stack representation for everything except the LAST operator,
                // which is stored as f.prevOp.
                f.rtermAssoc = new();
                f.rtermAssoc.Push((f.prevOp.Result.Value, rTerm));
                f.prevOp = rop2;
                loop_rtermStack: ;
                //Parse second term (required)
                //Right-assoc term prefix
                if (ParsePrefix() is { } prefixErrRS)
                    return prefixErrRS;
                //Delegate to higher priority operators for term proper
                f.stage = OperatorParseDelegation.DELEGATE_RTERMSTACK;
                evalStack.Push(f);
                f = new(f.opInd - 1, table);
                goto start;
                return_rtermStack: ;
                //Right-assoc term postfix
                if (ParsePostfix(out rTerm) is { } postfixErrRS)
                    return postfixErrRS;
                //Try to parse another operator
                rop2 = f.rassocP!(inp);
                if (rop2.Result.Valid) {
                    JoinParsed(rop2);
                    f.rtermAssoc!.Push((f.prevOp.Result.Value, rTerm));
                    f.prevOp = rop2;
                    //If there's another operator, loop back
                    goto loop_rtermStack;
                }
                //If there are no more operators, verify ambiguity, then collapse right-assoc terms
                if (VerifyAmbiguityAndLast(f, Associativity.Right, in f.prevOp, in rop2) is { } errRS)
                    return errRS;
                var op = f.prevOp.Result.Value;
                for (var (nextOp, nextRTerm) = f.rtermAssoc!.Pop(); ; (nextOp, nextRTerm) = f.rtermAssoc.Pop()) {
                    rTerm = op.Op(nextRTerm, rTerm);
                    op = nextOp;
                    if (f.rtermAssoc.Count == 0) break;
                }
                result = op.Op(f.term1, rTerm);
                continue;

                //Parse left associations
                lassoc_stage: ;
                if (f.lassocP is null)
                    goto nassoc_stage;
                //Left-assoc operator
                var rop = f.lassocP!(inp);
                if (rop.Status == ResultStatus.FATAL)
                    return rop.CastFailure<A>();
                if (!rop.Result.Valid)
                    goto nassoc_stage;
                lassoc_stage_inner: ;
                JoinParsed(f.prevOp = rop);
                //Parse first left-assoc term into lTerm
                //Left-assoc term prefix
                if (ParsePrefix() is { } prefixErrL)
                    return prefixErrL;
                //Delegate to higher priority operators for term proper
                f.stage = OperatorParseDelegation.DELEGATE_LTERM;
                evalStack.Push(f);
                f = new(f.opInd - 1, table);
                goto start;
                return_lterm: ;
                //Left-assoc term postfix
                if (ParsePostfix(out rTerm) is { } postfixErrL)
                    return postfixErrL;
                f.term1 = f.prevOp.Result.Value.Op(f.term1, rTerm);
                //Loop back and keep parsing left associations if another term exists
                rop = f.lassocP!(inp);
                if (rop.Status == ResultStatus.FATAL)
                    return rop.CastFailure<A>();
                if (!rop.Result.Valid) {
                    if (VerifyAmbiguityAndLast(f, Associativity.Left, in f.prevOp, rop) is { } errL)
                        return errL;
                    result = f.term1;
                    continue;
                }
                goto lassoc_stage_inner;

                //Parse nonassociative associations
                nassoc_stage: ;
                if (f.nassocP is null)
                    goto end;
                //First right-assoc operator
                f.prevOp = f.nassocP(inp);
                if (f.prevOp.Status == ResultStatus.FATAL)
                    return f.prevOp.CastFailure<A>();
                if (!f.prevOp.Result.Valid)
                    goto end;
                JoinParsed(f.prevOp);
                //Parse term into nTerm
                //Term prefix
                if (ParsePrefix() is { } prefixErrN)
                    return prefixErrN;
                //Delegate to higher priority operators for term proper
                f.stage = OperatorParseDelegation.DELEGATE_NTERM;
                evalStack.Push(f);
                f = new(f.opInd - 1, table);
                goto start;
                return_nterm: ;
                //Term postfix
                if (ParsePostfix(out rTerm) is { } postfixErrN)
                    return postfixErrN;
                if (VerifyAmbiguity(f, Associativity.None, in f.prevOp) is { } errN)
                    return errN;
                f.term1 = f.prevOp.Result.Value.Op(f.term1, rTerm);
                end: ;
                result = f.term1;
            }

            return new(result, prev!.Value.Error, prev.Value.Start, prev.Value.End);
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