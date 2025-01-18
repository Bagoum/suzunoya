using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using Scriptor;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Expressions;
using Scriptor.Math;
using Scriptor.Reflection;
using static Scriptor.Definition.Lexer;
using static Mizuhashi.Combinators;
using Op = Mizuhashi.Operator<Scriptor.Definition.Lexer.Token, Scriptor.Compile.ST, Scriptor.Definition.Lexer.Token>;

namespace Scriptor.Definition {
/// <summary>
/// Parser for BDSL2.
/// </summary>
public static class LangParser {
    /// <summary>
    /// If true, LStrings not found via <see cref="ILangCustomizer.TryFindLocalizedStringReference"/>
    ///  will be treated as unknown strings rather than parse errors.
    /// </summary>
    public static bool SoftFailOnUnmatchedLString { get; set; } = false;
    
    private static readonly Parser<Token, Token> IdentOrType = TokenOfTypes(TokenType.Identifier, TokenType.TypeIdentifier);
    private static readonly Parser<Token, Token?> TypeSuffixStr =
        op("::").IgThen(IdentOrType).OptN();
    
    /// <summary>
    /// Try to parse a string as a C# type; otherwise, return an error string.
    /// </summary>
    public static Either<Type, string> TypeFromString(string content) {
        var tDef = ParseType(content);
        if (tDef.IsRight)
            return tDef.Right;
        var typ = tDef.Left.TryCompile();
        if (typ.IsRight)
            return typ.Right;
        return typ.Left;
    }

    private static Either<(string meth, Type[] args), string> MaybeTypeArgsFromString(string content) {
        var tDef = ParseType(content);
        if (tDef.IsRight)
            return tDef.Right;
        if (tDef.Left is TypeDef.Atom at)
            return (at.Type, Array.Empty<Type>());
        if (tDef.Left is not TypeDef.Generic gen)
            return $"Cannot parse as type-args: {tDef.Left}";
        var args = new Type[gen.Args.Count];
        for (int ii = 0; ii < gen.Args.Count; ++ii) {
            var arg = gen.Args[ii].TryCompile();
            if (arg.IsRight)
                return arg.Right;
            args[ii] = arg.Left;
        }
        return (gen.Type, args);
    }

    internal static (string meth, Type[] args) TypeArgsFromString(string content) =>
        MaybeTypeArgsFromString(content).TryL(out var res) ? res : (content, Array.Empty<Type>());

    internal static Either<Type?, AST.Failure> TypeFromToken(Token? mtypStr, LexicalScope scope, bool allowVoid = false) {
        if (!mtypStr.Try(out var typStr))
            return null as Type;
        var result = TypeFromString(typStr.Content);
        if (!result.TryL(out var typ))
            return new AST.Failure(new ReflectionException(typStr.Position, result.Right), scope) 
                { IsTypeCompletion = true };
        if (typ == typeof(void) && !allowVoid)
            return new AST.Failure(new(typStr.Position, "Cannot instantiate a value of type void."), scope);
        return typ;
    }

    private static readonly Parser<Token, (Token id, Token? typ)> IdentAndType = 
        Ident.Then(TypeSuffixStr);
    
    private static readonly Parser<Token, Token> openParen = TokenOfType(TokenType.OpenParen);
    private static readonly Parser<Token, Token> closeParen = TokenOfType(TokenType.CloseParen);
    private static readonly Parser<Token, Token> openBrace = TokenOfType(TokenType.OpenBrace);
    private static readonly Parser<Token, Token> closeBrace = TokenOfType(TokenType.CloseBrace);
    private static readonly Parser<Token, Token> comma = TokenOfType(TokenType.Comma);
    private static readonly Parser<Token, Token> semicolon = TokenOfType(TokenType.Semicolon);

    private static Parser<Token, Unit> ImplicitBreak(TokenType breaker, bool allowEoF = false) {
        var err = new ParserError.Expected($"{breaker.ToString().ToLower()} or uniform newline indentation after previous line");
        return input => {
            if (input.Empty) {
                if (allowEoF)
                    return new(new(Unit.Default), null, input.Index, input.Index);
            } else {
                if (input.Next.Type == breaker)
                    return new(new(Unit.Default), null, input.Index, input.Step(1));
                else if ((input.Next.Flags & TokenFlags.ImplicitBreak) > 0)
                    return new(new(Unit.Default), null, input.Index, input.Index);
            }
            return new(err, input.Index);
        };
    }
    private static Parser<Token, Token> Kw(string keyword) => TokenOfTypeValue(TokenType.Keyword, keyword);
    private static Parser<Token, Token> Kws(params string[] keyword) => TokenOfTypeValues(TokenType.Keyword, keyword);
    private static Parser<Token, PositionRange> Kwp(string keyword) => 
        TokenOfTypeValue(TokenType.Keyword, keyword).FMap(t => t.Position);

    private static Parser<Token, Token> Flags(TokenFlags flags, string? expected = null) =>
        Satisfy((Token inp) => (inp.Flags & flags) > 0, expected).IsPresent();
    private static Parser<Token, Unit> NotFlags(TokenFlags flags, string? unexpected = null) =>
        Satisfy((Token inp) => (inp.Flags & flags) > 0).IsNotPresent(unexpected);

    private static readonly Parser<Token, Token> Whitespace = Flags(TokenFlags.PrecededByWhitespace, "whitespace");
    private static readonly Parser<Token, Unit> NoWhitespace = NotFlags(TokenFlags.PrecededByWhitespace, "whitespace");

    /// <summary>
    /// Create a <see cref="MethodSignature"/> for the method on type `t` named `method`.
    /// </summary>
    public static MethodSignature Meth(Type t, string method) =>
        MethodSignature.Get(ExFunction.WrapAny(t, method).Mi);
    private static ST FnIdentFor(Token t, params MethodSignature[] overloads) =>
        new ST.FnIdent(t.Position, overloads.Select(o => o.Call(t.Content)).ToArray());

    private static Func<ST, Token, ST, ST> InfixCallerEquivOverloads(params MethodSignature[] overloads) =>
        (a, t, b) => new ST.FunctionCall(a.Position.Merge(b.Position), FnIdentFor(t, overloads), a, b) { OverloadsInterchangeable = true };
    
    private static Parser<Token, Token> op(string op) => TokenOfTypeValue(TokenType.Operator, op);
    private static Parser<Token, Token> sop(string sop) => TokenOfTypeValue(TokenType.SpecialOperator, sop);
    private static Parser<Token, Token> opNoFlag(string op, TokenFlags f, string desc) => 
        TokenOfTypeValueNotFlag(TokenType.Operator, op, f, desc);

    private static Parser<Token, Token> infixOp(string op) =>
        opNoFlag(op, TokenFlags.ImplicitBreak, $"infix operator `{op}`");


    //specialized parser that gets one of many possible operators of equivalent priority/precedence,
    // and which doesn't provide LocatedParserError (since it is dropped in the operator table parser anyways)
    private static Parser<Token, Token> multiOpParser(TokenFlags notFlags, string[] ops) => inp => {
        if (inp.Empty)
            goto fail;
        var nxt = inp.Next;
        if (nxt.Type != TokenType.Operator || (nxt.Flags & notFlags) > 0)
            goto fail;
        foreach (var op in ops)
            if (nxt.Content == op)
                return new(new(nxt), null, inp.Index, inp.Step(1));
        fail: ;
        return ParseResult<Token>.SilentErr(inp.Index);
    };

    private static MethodSignature[] multiOpOverloads(Type cls, params string[] methNames) =>
        methNames.SelectToArr(m => Meth(cls, m));
    
    private static Op multiInfix(Associativity assoc, int precedence, string[] ops, MethodSignature[] overloads) {
        if (ops.Length != overloads.Length)
            throw new StaticException($"Incorrect infix count for {assoc}:{precedence}");
        return new Op.Infix(multiOpParser(TokenFlags.ImplicitBreak, ops), (a, t, b) =>
                new ST.FunctionCall(a.Position.Merge(b.Position), FnIdentFor(t, overloads[ops.IndexOf(t.Content)]), a, b),
            assoc, precedence
        );
    }
    
    //NB: It is critical to have the noWhitespace parse for +/- operators, because if we don't,
    // then curried function application of a unary number becomes higher precedence than arithmetic.
    // eg. `x - y` has higher precedence as Curried(x, Negate(y)) than Subtract(x, y).
    //F# handles this by parsing only no-whitespace +/- as unary.
    //It's not strictly necessary for the ! operator, but we require it for uniformity.
    private static Op multiPrefix(int precedence, string[] ops, MethodSignature[] overloads) {
        if (ops.Length != overloads.Length)
            throw new StaticException($"Incorrect prefix count for {precedence}");
        return new Op.Prefix(multiOpParser(TokenFlags.PostcededByWhitespace, ops), (t, x) =>
                new ST.FunctionCall(t.Position.Merge(x.Position), FnIdentFor(t, overloads[ops.IndexOf(t.Content)]), x),
            precedence
        );
    }
    private static Op multiPostfix(int precedence, string[] ops, MethodSignature[] overloads) {
        if (ops.Length != overloads.Length)
            throw new StaticException($"Incorrect postfix count for {precedence}");
        return new Op.Postfix(multiOpParser(TokenFlags.PrecededByWhitespace, ops), (x, t) =>
                new ST.FunctionCall(x.Position.Merge(t.Position), FnIdentFor(t, overloads[ops.IndexOf(t.Content)]), x),
            precedence
        );
    }
    
    private static FPrefix<Token,ST> fprefix(string op, MethodSignature meth) {
        var err = Lexer.ExpectNoFlag($"prefix operator `{op}`", TokenFlags.PostcededByWhitespace);
        return new(new(TokenType.Operator, new PositionRange(), op), (t, x) => 
                new ST.FunctionCall(t.Position.Merge(x.Position), FnIdentFor(t, meth), x),
            t => (t.Flags & TokenFlags.PostcededByWhitespace) == 0 ? null : err);
    }
    
    private static FPostfix<Token,ST> fpostfix(string op, MethodSignature meth) {
        var err = Lexer.ExpectNoFlag($"postfix operator `{op}`", TokenFlags.PrecededByWhitespace);
        return new(new(TokenType.Operator, new PositionRange(), op), (x, t) => 
                new ST.FunctionCall(x.Position.Merge(t.Position), FnIdentFor(t, meth), x),
            t => (t.Flags & TokenFlags.PrecededByWhitespace) == 0 ? null : err);
    }
    
    private static FInfix<Token,ST> finfix(string op, Associativity assoc, int precedence, MethodSignature meth, Func<ST,Token,ST,ST>? cons = null) {
        var err = Lexer.ExpectNoFlag($"infix operator `{op}`", TokenFlags.ImplicitBreak);
        return new(new(TokenType.Operator, new PositionRange(), op), cons ?? ((a, t, b) => 
                new ST.FunctionCall(a.Position.Merge(b.Position), FnIdentFor(t, meth), a, b)),
            assoc, precedence, t => (t.Flags & TokenFlags.ImplicitBreak) == 0 ? null : err);
    }
    private static FInfix<Token,ST> assigner(string op, string method) =>
        finfix(op, Associativity.Right, 2, Meth(typeof(ExMAssign), method));

    /*
    private static readonly Op[] tightOperators = [
        multiPostfix(20, ["++", "--"], multiOpOverloads(typeof(ExMAssign), 
            nameof(ExMAssign.PostIncrement), nameof(ExMAssign.PostDecrement))),
        
        multiPrefix(19, ["++", "--"], multiOpOverloads(typeof(ExMAssign),
            nameof(ExMAssign.PreIncrement), nameof(ExMAssign.PreDecrement))),
        
        multiPrefix(18, ["+", "-", "!"], multiOpOverloads(typeof(ExMOperators), 
            nameof(ExMOperators.ReturnSame), nameof(ExMOperators.Negate), nameof(ExMOperators.Not))),
    ];
    
    //these operators are lower precedence than curried function application.
    //eg. f x op y = f(x) op y
    private static readonly Op[] looseOperators = [
        multiInfix(Associativity.Left, 16, ["^", "^^", "^-"], 
            multiOpOverloads(typeof(ExMOperators), 
                nameof(ExMOperators.Pow), nameof(ExMOperators.NPow), nameof(ExMOperators.PowSub))),
        
        new Op.Infix(infixOp("*"), 
            InfixCallerEquivOverloads(
                //If we have two ints, then it's best to use MulInt instead of allowing conversion
                //If we have one int and a float, then we need to cast to MulFloat instead of using Mul/MulRev, which
                // produce incorrect (int,float)->int signatures
                Meth(typeof(ExMOperators), nameof(ExMOperators.MulFloat)),
                Meth(typeof(ExMOperators), nameof(ExMOperators.MulInt)),
                Meth(typeof(ExMOperators), nameof(ExMOperators.Mul)),
                Meth(typeof(ExMOperators), nameof(ExMOperators.MulRev))
            ), Associativity.Left, 14),
        multiInfix(Associativity.Left, 14, ["%", "/"], multiOpOverloads(typeof(ExMOperators), 
            nameof(ExMOperators.Modulo), nameof(ExMOperators.Div))),
        //infix("//", Associativity.Left, 14, Lift(typeof(ExM), nameof(ExM.FDiv))),

        multiInfix(Associativity.Left, 12, ["+", "-"], multiOpOverloads(typeof(ExMOperators), 
            nameof(ExMOperators.Add), nameof(ExMOperators.Sub))),

        multiInfix(Associativity.Left, 10, ["<", ">", "<=", ">="], multiOpOverloads(typeof(ExMOperators), 
                nameof(ExMOperators.Lt), nameof(ExMOperators.Gt),nameof(ExMOperators.Leq), nameof(ExMOperators.Geq))),

        multiInfix(Associativity.Left, 8, ["==", "!="], multiOpOverloads(typeof(ExMOperators), 
            nameof(ExMOperators.Eq), nameof(ExMOperators.Neq))),
        
        //& is defined to be the same as &&, not bitwise
        multiInfix(Associativity.Left, 6, ["&&", "||", "&", "|"], multiOpOverloads(typeof(ExMOperators),
            nameof(ExMOperators.And), nameof(ExMOperators.Or), nameof(ExMOperators.And), nameof(ExMOperators.Or))),
        
        new Op.Infix(TokenOfTypeValue(TokenType.Keyword, "as"), (obj, c, typ) => new ST.TypeAs(obj, c.Position, typ), Associativity.Left, 4),

        multiInfix(Associativity.Right, 2, ["=", "+=", "-=", "*=", "/=", "%=", "&=", "|="], multiOpOverloads(typeof(ExMAssign),
            nameof(ExMAssign.Assign), nameof(ExMAssign.AddAssign), nameof(ExMAssign.SubAssign), nameof(ExMAssign.MulAssign),
            nameof(ExMAssign.DivAssign), nameof(ExMAssign.ModAssign), nameof(ExMAssign.AndAssign), nameof(ExMAssign.OrAssign)))
    ];*/
    
    
    //prefix/postfix operators are higher precedence than curried function application.
    // eg. f x op y = f(x, op(y)) or f(op(x), y)
    // nb. x -y = x(-y), not x - y
    private static readonly FPrefix<Token, ST>[] fastPrefixOps = [
        fprefix("++", Meth(typeof(ExMAssign), nameof(ExMAssign.PreIncrement))),
        fprefix("--", Meth(typeof(ExMAssign), nameof(ExMAssign.PreDecrement))),
        fprefix("+", Meth(typeof(ExMOperators), nameof(ExMOperators.ReturnSame))),
        fprefix("-", Meth(typeof(ExMOperators), nameof(ExMOperators.Negate))),
        fprefix("!", Meth(typeof(ExMOperators), nameof(ExMOperators.Not))),
    ];
    private static readonly FPostfix<Token, ST>[] fastPostfixOps = [
        fpostfix("++", Meth(typeof(ExMAssign), nameof(ExMAssign.PostIncrement))),
        fpostfix("--", Meth(typeof(ExMAssign), nameof(ExMAssign.PostDecrement))),
    ];
    //infix operators are lower precedence than curried function application.
    //eg. f x op y = f(x) op y
    private static readonly FInfix<Token, ST>[] fastInfixOps = [
        finfix("^", Associativity.Left, 16, Meth(typeof(ExMOperators), nameof(ExMOperators.Pow))),
        finfix("^^", Associativity.Left, 16, Meth(typeof(ExMOperators), nameof(ExMOperators.NPow))),
        finfix("^-", Associativity.Left, 16, Meth(typeof(ExMOperators), nameof(ExMOperators.PowSub))),
        finfix("%", Associativity.Left, 14, Meth(typeof(ExMOperators), nameof(ExMOperators.Modulo))),
        
        //ideally for arithmetic operations we would statically lookup all the defined operators,
        // but that's kind of overkill, so we just include the basics:
        //  float*T, T*float, T/float, T+T, T-T, and the other fixed ones
        finfix("*", Associativity.Left, 14, null!, InfixCallerEquivOverloads(
            //If we have two ints, then it's best to use MulInt instead of allowing conversion
            //If we have one int and a float, then we need to cast to MulFloat instead of using Mul/MulRev, which
            // produce incorrect (int,float)->int signatures
            Meth(typeof(ExMOperators), nameof(ExMOperators.MulFloat)),
            Meth(typeof(ExMOperators), nameof(ExMOperators.MulInt)),
            Meth(typeof(ExMOperators), nameof(ExMOperators.Mul)),
            Meth(typeof(ExMOperators), nameof(ExMOperators.MulRev))
        )),
        finfix("/", Associativity.Left, 14, Meth(typeof(ExMOperators), nameof(ExMOperators.Div))),

        finfix("+", Associativity.Left, 12, Meth(typeof(ExMOperators), nameof(ExMOperators.Add))),
        finfix("-", Associativity.Left, 12, Meth(typeof(ExMOperators), nameof(ExMOperators.Sub))),

        finfix("<", Associativity.Left, 10, Meth(typeof(ExMOperators), nameof(ExMOperators.Lt))),
        finfix(">", Associativity.Left, 10, Meth(typeof(ExMOperators), nameof(ExMOperators.Gt))),
        finfix("<=", Associativity.Left, 10, Meth(typeof(ExMOperators), nameof(ExMOperators.Leq))),
        finfix(">=", Associativity.Left, 10, Meth(typeof(ExMOperators), nameof(ExMOperators.Geq))),

        finfix("==", Associativity.Left, 8, Meth(typeof(ExMOperators), nameof(ExMOperators.Eq))),
        finfix("!=", Associativity.Left, 8, Meth(typeof(ExMOperators), nameof(ExMOperators.Neq))),
        //& is defined to be the same as &&, not bitwise
        finfix("&&", Associativity.Left, 6, Meth(typeof(ExMOperators), nameof(ExMOperators.And))),
        finfix("||", Associativity.Left, 6, Meth(typeof(ExMOperators), nameof(ExMOperators.Or))),
        finfix("&", Associativity.Left, 6, Meth(typeof(ExMOperators), nameof(ExMOperators.And))),
        finfix("|", Associativity.Left, 6, Meth(typeof(ExMOperators), nameof(ExMOperators.Or))),
        
        finfix("as", Associativity.Left, 4, null!, (obj, c, typ) => new ST.TypeAs(obj, c.Position, typ)),

        assigner("=", nameof(ExMAssign.Assign)),
        assigner("+=", nameof(ExMAssign.AddAssign)),
        assigner("-=", nameof(ExMAssign.SubAssign)),
        assigner("*=", nameof(ExMAssign.MulAssign)),
        assigner("/=", nameof(ExMAssign.DivAssign)),
        assigner("%=", nameof(ExMAssign.ModAssign)),
        assigner("&=", nameof(ExMAssign.AndAssign)),
        assigner("|=", nameof(ExMAssign.OrAssign))
    ];

    private static Parser<Token, T> Paren1<T>(Parser<Token, T> p) {
        var openErr = new ParserError.Expected($"{TokenType.OpenParen}");
        var closeErr = new ParserError.Expected($"{TokenType.CloseParen}");
        return inp => {
            var starti = inp.Index;
            if (inp.Empty || inp.Next.Type != TokenType.OpenParen)
                return new(openErr, starti);
            inp.Step();
            var rval = p(inp);
            if (!rval.Result.Valid)
                return new(rval.Result, rval.Error, starti, rval.End);
            if (inp.Empty || inp.Next.Type != TokenType.CloseParen)
                return new(new LocatedParserError(inp.Index, closeErr), starti, inp.Index);
            inp.Step();
            return new(rval.Result, rval.Error, starti, inp.Index);
        };
    }
    
    private static Parser<Token, (PositionRange allPosition, List<T> args)> Paren<T>(Parser<Token, T> p, Parser<Token, T>? first = null, bool atleastOne = false) {
        var args = p.SepBy(comma, atleastOne: atleastOne, first: first);
        var openErr = new ParserError.Expected($"{TokenType.OpenParen}");
        var closeErr = new ParserError.Expected($"{TokenType.CloseParen}");
        return inp => {
            var starti = inp.Index;
            if (inp.Empty || inp.Next.Type != TokenType.OpenParen)
                return new(openErr, starti);
            inp.Step();
            var rval = args(inp);
            if (!rval.Result.Valid)
                return new(rval.Error, starti, rval.End);
            if (inp.Empty || inp.Next.Type != TokenType.CloseParen)
                return new(new LocatedParserError(inp.Index, closeErr), starti, inp.Index);
            inp.Step();
            return new((inp.Source[starti].Position.Merge(inp.Source[inp.Index-1].Position), rval.Result.Value), 
                rval.Error, starti, inp.Index);
        };
    }

    private static readonly ParserError blockKWErr = new ParserError.Expected("`block` or `b{` keyword");
    private static readonly Parser<Token, Token> blockKW = input => {
        if (input.Empty || input.Next.Type != TokenType.Keyword || (input.Next.Content != "block" && input.Next.Content != "b"))
            return new(blockKWErr, input.Index);
        else
            return new(new(input.Next), null, input.Index, input.Step(1));
    };
    
    //Atom: identifier; number/string/etc; parenthesized value; block 
    private static readonly Parser<Token, ST> atom = Choice(
        //Identifier, optionally with type specification
        IdentAndType.FMap(idtyp => new ST.Ident(idtyp.id, idtyp.typ) as ST),
        //Identifier or type identifier (type identifier as atom is required to support `as` operator)
        IdentOrType.FMap(id => new ST.Ident(id) as ST),
        //num/string/v2rv2
        Num.FMap(t => new ST.Number(t.Position, t.Content) as ST),
        TokenOfType(TokenType.ValueKeyword).FMap(t => t.Content switch {
            "true" => new ST.TypedValue<bool>(t.Position, true) { Kind = SymbolKind.Boolean } as ST,
            "false" => new ST.TypedValue<bool>(t.Position, false) { Kind = SymbolKind.Boolean },
            _ => new ST.Failure(t.Position, $"Unknown value keyword {t.Content}. Please report this.")
        }),
        TokenOfType(TokenType.NullKeyword).Then(TypeSuffixStr).FMap(t => 
            new ST.DefaultValue(t.a.Position, t.b) as ST),
        TokenOfType(TokenType.DefaultKeyword).FMap(t => 
            new ST.DefaultValue(t.Position, null, asFunctionArg: true) as ST),
        TokenOfType(TokenType.String).FMap(t => new ST.TypedValue<string>(t.Position, t.Content) 
            { Kind = SymbolKind.String} as ST),
        TokenOfType(TokenType.Char).FMap(t => t.Content.Length == 1 ?
            new ST.TypedValue<char>(t.Position, t.Content[0]) { Kind = SymbolKind.String} :
            new ST.Failure(t.Position, "A character literal must be exactly one character long.") as ST),
        TokenOfType(TokenType.LString).Bind(t => {
            LString v;
            var diagnostics = Array.Empty<ReflectDiagnostic>();
            var serv = ServiceLocator.Find<ILangCustomizer>();
            if (serv.TryFindLocalizedStringReference(t.Content) is { } ls)
                v = ls;
            else if (SoftFailOnUnmatchedLString) {
                v = $"Unresolved LocalizedString {t.Content}";
                diagnostics = [new ReflectDiagnostic.Warning(t.Position,
                        $"Couldn't resolve LocalizedString {t.Content}. It may work properly in-game.")];
            } else
                return new ParseResult<ST>(
                    new ParserError.Failure($"Couldn't resolve LocalizedString {t.Position}"),
                    t.Position.Start.Index, t.Position.End.Index);
            return new ParseResult<ST>(new ST.TypedValue<LString>(t.Position, v) {
                Kind = SymbolKind.String,
                Diagnostics = diagnostics
            }, null, t.Position.Start.Index, t.Position.End.Index);
        }),
        TokenOfType(TokenType.V2RV2).FMap(t => new ST.TypedValue<V2RV2>(t.Position, SimpleParser.ParseV2RV2(t.Content)) 
                { Kind = SymbolKind.Number } as ST),
        //Parenthesized value/tuple
        Paren(ValueOrFailure, Value).FMap(vs => vs.args.Count == 1 ? vs.args[0] : new ST.Tuple(vs.allPosition, vs.args)).LabelV("tuple/parentheses"),
        //Block
        Sequential(blockKW, BracedBlock,
            (o, b) => b with { Position = o.Position.Merge(b.Position) } as ST).LabelV("block"),
        //Array
        Sequential(openBrace, ((Parser<Token,ST>)Value).SepBy(ImplicitBreak(TokenType.Comma)), closeBrace,
            TypeSuffixStr, (o, vals, c, typ) => new ST.Array(o.Position.Merge(c.Position), typ, vals.ToArray()) as ST)
            .LabelV("array")
    );

    private static readonly ParserError.Expected termMemberFollowErr =
        new("member access x.y and/or function application f(x, y)");
    private static readonly ParserError.Expected termMemberGenericErr =
        new("generic function x.y<T>()");
    private static readonly Parser<Token, Token> period = op(".");
    
    private static readonly Parser<Token, (PositionRange, List<ST>)> parenArgs =
        Paren(ValueOrFailure, Value);

    private static readonly Parser<Token, (Maybe<Token>, Maybe<(PositionRange, List<ST>)>)> termMemberFnFollow = inp => {
            ParseResult<(Maybe<Token>, Maybe<(PositionRange, List<ST>)>)> Fail<T>(ParseResult<T> res, ParserError.Expected? wrapErr = null) {
                var nerr = res.Error;
                if (wrapErr is not null) {
                    if (res.Error is { } lerr)
                        nerr = lerr.WithError(new ParserError.Labelled(wrapErr.String, lerr.Error));
                    else
                        nerr = new(res.Start, res.End, wrapErr);
                }
                return new(nerr, res.Start, res.End);
            }
            var per = period(inp);
            var field = !per.Result.Try(out var p) ?
                per :
                IdentOrType(inp) is { Status: not ResultStatus.ERROR } rid ?
                    rid.WithPreceding(per) :
                    //Allow empty identifier here-- it will fail during annotation, but it permits better errors
                    new(new Token(TokenType.Identifier, p.Position.End.EmptyRange(), ""), null, per.Start, per.End);
            if (field.Status == ResultStatus.FATAL)
                return Fail(field, termMemberFollowErr);
            var func = !inp.Empty && !inp.Next.Flags.HasFlag(TokenFlags.PrecededByWhitespace) ?
                parenArgs(inp).AsNullable() :
                null;
            if (func is {Status: ResultStatus.FATAL} _fn)
                return Fail(_fn.WithPreceding(field), termMemberFollowErr);
            /*if (field.Status is ResultStatus.ERROR && func?.Status is null or ResultStatus.ERROR) {
                //fail silently
                return ParseResult<(Maybe<Token>, Maybe<(PositionRange, List<ST>)>)>.SilentErr(inp.Index);
            }*/
            if (func?.Status != ResultStatus.OK) {
                if (!field.Result.Try(out var fieldres)) {
                    return Fail(field.WithNextNullable(func).AsSameError(termMemberFollowErr));
                } else if (fieldres.Type is TokenType.TypeIdentifier) {
                    return Fail(field.WithNextNullable(func).AsSameError(termMemberGenericErr, false));
                }
            }
            return func is { } fn ?
                new((field.Result, fn.Result), field.MergeErrors(fn), field.Start, fn.End) :
                new((field.Result, Maybe<(PositionRange, List<ST>)>.None), field.Error, field.Start, field.End);
        };
    
    //Term: member access `x.y`, member function `x.f(y)`, indexer `x[y]`,
    // constructor `new X(y)`, C#-style function application `f(x, y)`, partial function call `$(f, x)`,
    //Haskell-style function application `f x y` is handled in term2.
    private static readonly Parser<Token, ST> term =
        ChoiceL("term (identifier, number, array, block, parenthesized expression, constructor, member/indexer access, or partial function)",
            Sequential(Kw("new"), IdentOrType, Paren(ValueOrFailure, Value), ST (kw, typ, args) => 
                    new ST.Constructor(kw.Position.Merge(args.allPosition), kw.Position, typ, args.args.ToArray()))
                .LabelV("constructor"),
            Sequential(atom, Combinators.Either(
                    termMemberFnFollow,
                    Sequential(TokenOfType(TokenType.OpenBracket), ValueOrFailure, TokenOfType(TokenType.CloseBracket),
                        (open, val, close) => (open, val, close)).LabelV("indexer x[i]") ).Many(silence: true),
                (x, seqs) => {
                    for (int ii = 0; ii < seqs.Count; ++ii) {
                        if (seqs[ii].TryR(out var indexer)) {
                            x = new ST.Indexer(x, indexer.open.Position, indexer.val, indexer.close.Position);
                        } else {
                            var (mem, fn) = seqs[ii].Left;
                            if (mem.Try(out var m))
                                if (fn.Try(out var f))
                                    x = new ST.MemberFunction(x.Position.Merge(f.Item1), x, new ST.Ident(m), f.Item2.ToArray());
                                else
                                    x = new ST.MemberAccess(x, new ST.Ident(m));
                            else if (fn.Try(out var f))
                                x = new ST.FunctionCall(x.Position.Merge(f.Item1), x, f.Item2.ToArray());
                        }
                    }
                    return x;
                }
            ),
            sop("$").IgThen(NoWhitespace).IgThen(
                Paren(ValueOrFailure, Value, atleastOne: true).FMap(ST (pa) => 
                    new ST.PartialFunctionCall(pa.allPosition, pa.args[0], pa.args.Skip(1).ToArray())
                )).LabelV("partial function application $(f,x,y..)")
        );

    //Term + tight operators
    private static readonly Parser<Token, ST> termOps1 =
        ParsePrefixPostfixFast(term, new TokenMatchEq(), fastPrefixOps, fastPostfixOps);
        //ParseOperators(tightOperators, term);

    private static readonly Parser<Token, Token> curryFnAppSep = NotFlags(TokenFlags.ImplicitBreak, 
        "indent: curried function application across newlines must change the indentation level").IgThen(Whitespace);
    
    //Term + tight operators + curried function application
    private static readonly Parser<Token, ST> term2 = inp => {
        var rf = termOps1(inp);
        //First element must be Ident for curried functions
        if (!rf.Result.Try(out var f) || f is not ST.Ident)
            return rf;
        var start = rf.Start;
        while (true) {
            var rsep = curryFnAppSep(inp);
            //this separator cannot fatal
            if (rsep.Status != ResultStatus.OK)
                return new(f, rf.MergeErrors(rsep), start, rf.End);
            rf = termOps1(inp);
            if (rf.Status == ResultStatus.FATAL)
                return rf;
            if (rf.Status == ResultStatus.ERROR && !rsep.Consumed)
                return new(f, rsep.MergeErrors(rf), start, rf.End);
            f = new ST.CurriedFunctionCall(f, rf.Result.Value);
        }
    };

    //Loose operators
    //Note that this would be called an "expression" in most parsers but I won't call it that to avoid ambiguity
    // with Linq.Expression
    private static readonly Parser<Token, ST> term2Ops =
        ParseOperatorsFast(term2, new TokenMatchEq(), fastInfixOps);
        //ParseOperators(looseOperators, term2);

    //Conditional expression
    private static readonly Parser<Token, ST> value = Sequential(
        term2Ops,
        op("?").IgThen(term2Ops).ThenIg(op(":")).Then(term2Ops).Opt(silence: true),
        (a, b) => {
            if (!b.Try(out var rest))
                return a;
            return new ST.IfExpression(a, rest.a, rest.b);
        }
    );
    
    private static ParseResult<ST> Value(InputStream<Token> inp) => value(inp);
    private static readonly ParserError noValueExpr = new ParserError.Expected("value expression");
    private static ParseResult<ST> ValueOrFailure(InputStream<Token> inp) {
        var rv = value(inp);
        if (rv.Status != ResultStatus.ERROR)
            return rv;
        var pos = inp.Remaining > 0 ? inp.Next.Position : inp.Source[^1].Position;
        return new ParseResult<ST>(Maybe<ST>.Of(new ST.Failure(pos, "No value expression provided")), 
            new LocatedParserError(inp.Index, noValueExpr), inp.Index, inp.Index);
    }

    private static readonly Parser<Token, ST.VarDeclAssign> varInit =
        Sequential(Kws("var", "hvar"), IdentAndType, op("="), value,
            (kw, idTyp, eq, res) => {
                var (id, typ) = idTyp;
                return new ST.VarDeclAssign(kw, id, typ, eq.Position, res);
            });

    private static readonly Parser<Token, ST.FunctionDef> functionDecl =
        Sequential(Kws("function", "hfunction"), Lexer.Ident,
            Paren(IdentAndType.Then(op("=").Then(value).OptN())), 
            TypeSuffixStr, BracedBlock,
            (fn, name, args, type, body) => new ST.FunctionDef(fn, name, args.args, type, body));

    private static readonly Parser<Token, ST> macroDecl =
        Sequential(Kw("macro"), Lexer.Ident, 
            Paren(Ident.Then(op("=").Then(value).OptN())), 
            BracedBlock,
            (fn, name, args, body) => new ST.MacroDef(fn, name, args.args, body) as ST);

    private static readonly Parser<Token, ST> ifElseStatement =
        Sequential(Kw("if"), Paren1(value), BracedBlock, Kw("else")
                .Then(Combinators.FMap<Token, ST.Block, ST>(BracedBlock, b => b).Or(IfElseStatement)).Opt()
            , (kwif, cond, iftrue, iffalse) => {
                var els = iffalse.ValueOrSNull();
                return new ST.IfStatement(kwif.Position, els?.a.Position, cond, iftrue, els?.b) as ST;
            });
    private static ParseResult<ST> IfElseStatement(InputStream<Token> inp) => ifElseStatement(inp);
    
    //Statement: value, variable declaration, special statement, or void-type block (func decl, if/else, for, while)
    private static readonly Parser<Token, ST> statement = ChoiceL("statement",
        value,
        Sequential(Kw("const").OptN(), Combinators.Either(varInit, functionDecl), (cnst, succ) => {
            if (succ.IsLeft) {
                succ.Left.ConstKwPos = cnst?.Position;
                return succ.Left as ST;
            } else {
                succ.Right.ConstKwPos = cnst?.Position;
                return succ.Right;
            }
        }),
        macroDecl,
        Sequential(Kw("return"), value.Opt(), (kw, v) => new ST.Return(kw.Position, v.ValueOrNull()) as ST),
        Kw("continue").FMap(t => new ST.Continue(t.Position) as ST),
        Kw("break").FMap(t => new ST.Break(t.Position) as ST),
        IfElseStatement,
        Sequential(Kw("for"), Paren1(Sequential(
                Combinators.Opt<Token, ST>(Statement),
                TokenOfType(TokenType.Semicolon),
                value.Opt(),
                TokenOfType(TokenType.Semicolon),
                Combinators.Opt<Token, ST>(Statement),
                (initial, _, cond, _, final) => (initial.ValueOrNull(), cond.ValueOrNull(), final.ValueOrNull())
            )), BracedBlock,
            (kw, checks, body) => new ST.Loop(kw.Position, checks.Item1, checks.Item2, checks.Item3, body) as ST
        ),
        Sequential(Kw("while"), Paren1(value), BracedBlock, 
            (kw, check, body) => new ST.Loop(kw.Position, null, check, null, body) as ST)
    );
    private static ParseResult<ST> Statement(InputStream<Token> inp) => statement(inp);
    private static readonly Parser<Token, List<ST>> statements = 
        statement.ThenIg(ImplicitBreak(TokenType.Semicolon, allowEoF: true)).Many()
            .Label("statement block");
    private static readonly Parser<Token, ST.Block> bracedBlock =
        Sequential(openBrace, statements, closeBrace,
            (o, stmts, c) => new ST.Block(o.Position.Merge(c.Position), stmts));
    private static ParseResult<ST.Block> BracedBlock(InputStream<Token> inp) => bracedBlock(inp);
    private static readonly Parser<Token, List<ST>> imports =
        Sequential(
            Kw("import"), 
            Lexer.Ident,
            Kwp("at").Then(TokenOfType(TokenType.String)).Opt(),
            Kwp("as").Then(Lexer.Ident).Opt(),
            (kw, id, loc, alias) => new ST.Import(kw.Position, id, loc.ValueOrSNull(), alias.ValueOrSNull()) as ST).
                ThenIg(ImplicitBreak(TokenType.Semicolon, allowEoF: true)).Many(silence: true)
                    .LabelV("imports");

    private static readonly Parser<Token, ST.Block> fullScript = 
        imports.Then(statements).ThenIg(EOF<Token>()).FMap(x => new ST.Block(x.a.Concat(x.b).ToList()));

    /// <summary>
    /// Parse the provided tokens into a syntax tree.
    /// </summary>
    public static Either<ST.Block, LocatedParserError> Parse(string source, Token[] tokens, out InputStream<Token> stream) {
        var result = fullScript(stream = new InputStream<Token>(
            tokens, "BDSL2 parser", witness: new TokenWitnessCreator(source)));
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            (result.Error ?? new(0, new ParserError.Failure("Parsing failed, but it's unclear why.")));
    }

    /// <summary>
    /// A descriptor of an uncompiled type.
    /// </summary>
    public abstract record TypeDef {
        private static Dictionary<int, Dictionary<string, Type>>? _genericToType;
        /// <summary>
        /// Mapping from count to all types with that number of generic type parameters.
        /// </summary>
        public static Dictionary<int, Dictionary<string, Type>> GenericToType => _genericToType ??= LoadTypes();

        private static Dictionary<int, Dictionary<string, Type>> LoadTypes() {
            var d = new Dictionary<int, Dictionary<string, Type>>();
            void AddType(Type typ) {
                var name = typ.Name;
                var generic = 0;
                if (name.Contains('`')) {
                    var parts = name.Split('`', StringSplitOptions.RemoveEmptyEntries);
                    name = parts[0];
                    generic = int.Parse(parts[1]);
                }
                d.Add2(generic, name, typ);
            }
            foreach (var typ in AppDomain.CurrentDomain.GetAssemblies().Where(a => {
                         if (a.IsDynamic) return false;
                         var n = a.GetName().Name!;
                         return n == "mscorlib" || n == "System.Private.CoreLib" || n == "System.Core" || 
                                n == "UnityEngine.CoreModule" || n == "System.Linq" ||
                                n.StartsWith("Danmokou") || n == "BagoumLib" || n == "Suzunoya" || n == "Mizuhashi";
                     }).SelectMany(a => a.GetExportedTypes()).Where(t => {
                         if (t.Name.EndsWith("Attribute") || t.Name.Contains("Unsafe")) return false;
                         if (t.Namespace is { } ns) {
                             if (ns.StartsWith("System.Runtime")) return false;
                         }
                         return true;
                     })) {
                AddType(typ);
            }
            AddType(typeof(Unit));
            return d;
        }
        
        /// <summary>
        /// Compile this descriptor into a type, or return an error message.
        /// </summary>
        public abstract Either<Type, string> TryCompile();

        /// <summary>
        /// An atomic type.
        /// </summary>
        public record Atom(string Type) : TypeDef {
            private static readonly Dictionary<string, Type> simpleNameTypes =
                CSharpTypePrinter.SimpleTypeNameMap.ToDictionary(kv => kv.Value, kv => kv.Key);

            /// <inheritdoc/>
            public override Either<Type, string> TryCompile() {
                if (simpleNameTypes.TryGetValue(Type, out var t))
                    return t;
                if (GenericToType[0].TryGetValue(Type, out t))
                    return t;
                return $"Couldn't recognize type {Type}.";
            }
        }

        /// <summary>
        /// A type T&lt;T1,T2..&gt;
        /// </summary>
        public record Generic(string Type, List<TypeDef> Args) : TypeDef {
            /// <inheritdoc/>
            public override Either<Type, string> TryCompile() {
                if (GenericToType.TryGet2(Args.Count, Type, out var t)) {
                    return Args
                        .SequenceL(a => a.TryCompile())
                        .FMapL(args => t.MakeGenericType(args.ToArray()));
                }
                var arg = Args.Count == 1 ? "" : "s";
                return $"Couldn't recognize type constructor {Type} with {Args.Count} argument{arg}.";
            }
        }

        /// <summary>
        /// A type T[].
        /// </summary>
        /// <param name="Arg"></param>
        public record Array(TypeDef Arg) : TypeDef {
            /// <inheritdoc/>
            public override Either<Type, string> TryCompile() => Arg.TryCompile().FMapL(a => a.MakeArrayType());
        }
    }


    private static readonly Parser<char, TypeDef> typeParser =
        Sequential(
            Sequential(Satisfy(char.IsLetter), ManySatisfy(c => char.IsLetterOrDigit(c) || c == '_'), (a, b) => $"{a}{b}")
                .Label("simple type name"),
            Combinators.Between('<', ((Parser<char, TypeDef>)TypeParser).SepBy1(Char(',').IgThen(WhitespaceIL)), '>').Opt(),
            Combinators.Between('[', WhitespaceIL, ']').Many(),
            (x, gen, arr) => {
                var td = gen.Try(out var g) ? new TypeDef.Generic(x, g) : new TypeDef.Atom(x) as TypeDef;
                for (int ii = 0; ii < arr.Count; ++ii)
                    td = new TypeDef.Array(td);
                return td;
            }
        ).Label("type");
    
    private static ParseResult<TypeDef> TypeParser(InputStream<char> inp) => typeParser(inp);
    
    private static Either<TypeDef, string> ParseType(string source) {
        var strm = new InputStream<char>(source, "Type parser");
        var result = typeParser(strm);
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            (result.Error ?? new(0, new ParserError.Failure("This is not a valid type definition."))).Show(strm);
    }
}
}