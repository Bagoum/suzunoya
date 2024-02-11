using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace BagoumLib.Expressions {
/// <summary>
/// Linearizes expressions.
/// <br/>A linearized expression is one of:
/// <br/>(1) an expression that can participate in basic C# value operations (eg. can be the X in `return 2 + X;`),
/// <br/>(2) a BlockExpression whose statements satisfy (1) or (3) but are *not* BlockExpressions,
///  and whose last statement only satisfies (1).
/// <br/>(3) an expression with a return type of void (such as if/try/switch, or the elusive void BlockExpression),
///  whose children are all linearized expressions.
/// <br/>Linearized expressions can be converted to source code without too much difficulty. See <see cref="PrintVisitor"/>.
/// </summary>
public class LinearizeVisitor : ExpressionVisitor {

    private int counter = 0;

    /// <summary>
    /// If true, will assume that any expression can throw an exception,
    ///  and as a result will always assign block results to temporary variables.
    /// </summary>
    public bool SafeExecution { get; set; } = false;

    private static bool MightThrowOrChange(Expression? ex) => ex switch {
        null => false,
        ConstantExpression => false,
        DefaultExpression => false,
        //Parameters may be reassigned, but the $flat vars we use for linearization
        // are only assigned once
        ParameterExpression pex => pex.Name?.StartsWith("$flat") is not true,
        _ => true
    };
    private Expression Linearize(Func<Expression, Expression> combiner, Expression? piece) =>
        Linearize(exprs => combiner(exprs[0]), new[] {piece}, false);
    private Expression Linearize(Func<Expression[], Expression> combiner, Expression?[] pieces, bool allowTempAssign=true) {
        Expression?[] linearized = pieces.Select(Visit).ToArray();
        if (!linearized.Any(ex => ex is BlockExpression))
            return combiner(linearized!);
        var parameters = new List<ParameterExpression>();
        var statements = new List<Expression>();
        var lastStatements = new Expression?[linearized.Length];
        var consumes = new EnumerateVisitor();

        var useTemp = allowTempAssign && (SafeExecution || linearized.Any(l => 
            l is not null && consumes.Enumerate(l)
                .Any(e => {
                    if (e.NodeType.IsAssign()) {
                        ParameterExpression? assignTo;
                        if (e is BinaryExpression { Left: ParameterExpression pex })
                            assignTo = pex;
                        else if (e is UnaryExpression { Operand: ParameterExpression pex_ })
                            assignTo = pex_;
                        else
                            return true;
                        //assignment operations in blocks require full linearization,
                        // unless the assignment is block-local
                        //eg. x + { x = 2; x + 3 } requires tempvars
                        //    x + { int y = 2; x + y } does not require tempvars
                        return !(l is BlockExpression bex && bex.Variables.Contains(assignTo));
                    }
                    //order of method calls and throws must be preserved
                    return e is MethodCallExpression or UnaryExpression { NodeType: ExpressionType.Throw };
                })));

        for (int i = 0; i < linearized.Length; ++i) {
            if (linearized[i] is BlockExpression bex) {
                parameters.AddRange(bex.Variables);
                statements.AddRange(bex.Expressions.Take(bex.Expressions.Count - 1));
                lastStatements[i] = bex.Expressions[^1];
            } else
                lastStatements[i] = linearized[i];
            if (useTemp && linearized[i] != null && MightThrowOrChange(lastStatements[i])) {
                var tmp = Expression.Parameter(linearized[i]!.Type, $"$flatBlock{counter++}");
                parameters.Add(tmp);
                statements.Add(Ex.Assign(tmp, lastStatements[i]!));
                lastStatements[i] = tmp;
            }
        }

        statements.Add(combiner(lastStatements!));
        return Ex.Block(parameters, statements);
    }


    /// <summary>
    /// Invariant: expr is linearized under (1) or (2).
    /// <br/>Returns a linearized expression under (1) or (2) equivalent to expr that,
    ///  as a side effect, assigns the value of expr to dst.
    /// </summary>
    private Ex WithAssign(Ex expr, ParameterExpression dst) {
        if (expr is BlockExpression bex) {
            var exprs = bex.Expressions.ToArray();
            //Don't need to assign if throwing
            if (exprs[^1] is UnaryExpression {NodeType: ExpressionType.Throw})
                return expr;
            exprs[^1] = Ex.Assign(dst, exprs[^1]);
            return Ex.Block(bex.Variables, exprs);
        } else {
            if (expr is UnaryExpression {NodeType: ExpressionType.Throw})
                return expr;
            return Ex.Assign(dst, expr);
        }
    }

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node) {
        return node.NodeType switch {
            // A && B
            // A ? B : false;
            ExpressionType.AndAlso => Visit(Ex.Condition(node.Left, node.Right, Ex.Constant(false))),
            // A || B
            // A ? true : B;
            ExpressionType.OrElse => Visit(Ex.Condition(node.Left, Ex.Constant(true), node.Right)),
            _ => Linearize(exs => Ex.MakeBinary(node.NodeType, exs[0], exs[1]), new[] {node.Left, node.Right},
                !node.NodeType.IsAssign())
        };
    }

    /// <inheritdoc />
    protected override Expression VisitBlock(BlockExpression node) {
        if (node.Expressions.Count == 1 && node.Variables.Count == 0)
            return Visit(node.Expressions[0]);
        var prms = new List<ParameterExpression>();
        var stmts = new List<Expression>();
        prms.AddRange(node.Variables);
        foreach (var ex in node.Expressions) {
            var linearized = Visit(ex);
            if (linearized is BlockExpression bex) {
                prms.AddRange(bex.Variables);
                stmts.AddRange(bex.Expressions);
            } else {
                stmts.Add(linearized);
            }
        }
        return Ex.Block(prms, stmts);
    }

    /// <inheritdoc />
    protected override CatchBlock VisitCatchBlock(CatchBlock node) {
        Expression? filter = null;
        if (node.Filter != null)
            filter = Visit(node.Filter);
        if (filter?.IsBlockishExpression() is true)
            throw new Exception("Cannot have a block-like expression in catch filter");
        return Ex.MakeCatchBlock(node.Test, node.Variable, Visit(node.Body), filter);
    }

    /// <inheritdoc />
    protected override Expression VisitConditional(ConditionalExpression node) {
        if (node.Type == typeof(void))
            //If/then statements only require fixing the condition, since if statements can take blocks as children
            return Linearize(cond => Ex.Condition(cond, Visit(node.IfTrue), Visit(node.IfFalse), node.Type), node.Test);
        
        var ifT = Visit(node.IfTrue);
        var ifF = Visit(node.IfFalse);
        if (ifF is BlockExpression || ifT is BlockExpression) {
            //This handling is more complex than the standard handling since it'd be incorrect
            // to just evaluate both branches and return the correct one.
            //Instead, we declare a variable outside an if statement, and write to it in the branches.
            var prm = Ex.Parameter(node.Type, $"$flatTernary{counter++}");
            return Ex.Block(new[] {prm},
                Linearize(cond => Ex.Condition(cond, WithAssign(ifT, prm), WithAssign(ifF, prm), typeof(void)), node.Test),
                prm
            );
        } else
            return Linearize(cond => Ex.Condition(cond, ifT, ifF, node.Type), node.Test);
    }
    
    //Constant, default, dynamic, elementInit, extension, goto: no changes

    /// <inheritdoc />
    protected override Expression VisitIndex(IndexExpression node) =>
        Linearize(args => Ex.MakeIndex(args[0], node.Indexer, args.Skip(1)), 
            node.Arguments.Prepend(node.Object).ToArray());

    /// <inheritdoc />
    protected override Expression VisitInvocation(InvocationExpression node) =>
        Linearize(args => Ex.Invoke(args[0], args.Skip(1)), node.Arguments.Prepend(node.Expression).ToArray());
    
    //label, labeltarget, lambda: no changes

    /// <inheritdoc />
    protected override Expression VisitListInit(ListInitExpression node) {
        throw new Exception();
    }

    //loop: no changes. TODO: is it possible for loops to have values in C#?

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node) =>
        Linearize(expr => Ex.MakeMemberAccess(expr, node.Member), node.Expression);

    private (MemberBinding binding, BlockExpression? block) LinearizeMemberBinding(MemberBinding m) {
        if (m is MemberAssignment ma) {
            return Visit(ma.Expression) switch {
                BlockExpression bex => (Ex.Bind(ma.Member, bex.Expressions[^1]), bex),
                { } ex => (Ex.Bind(ma.Member, ex), null)
            };
        }
        //membermemberbinding, memberlistbinding
        throw new Exception($"Member binding not handled: {m.BindingType}");
    }

    /// <inheritdoc />
    protected override Expression VisitMemberInit(MemberInitExpression node) {
        var newExpr = Visit(node.NewExpression);
        var bindings = node.Bindings.Select(LinearizeMemberBinding).ToArray();
        var prms = new List<ParameterExpression>();
        var stmts = new List<Expression>();
        Expression newExprLast = newExpr;
        if (newExpr is BlockExpression b) {
            prms.AddRange(b.Variables);
            stmts.AddRange(b.Expressions.Take(b.Expressions.Count - 1));
            newExprLast = b.Expressions[^1];
        }
        foreach (var (_, block) in bindings)
            if (block != null) {
                prms.AddRange(block.Variables);
                stmts.AddRange(block.Expressions.Take(block.Expressions.Count - 1));
            }
        
        stmts.Add(Ex.MemberInit((newExprLast as NewExpression)!, bindings.Select(bd => bd.binding)));
        return Ex.Block(prms, stmts);
    }

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node) =>
        Linearize(args => Ex.Call(node.Object, node.Method, args), node.Arguments.ToArray());

    /// <inheritdoc />
    protected override Expression VisitNew(NewExpression node) =>
        Linearize(args => Ex.New(node.Constructor, args), node.Arguments.ToArray());

    /// <inheritdoc />
    protected override Expression VisitNewArray(NewArrayExpression node) =>
        Linearize(args => 
            node.NodeType == ExpressionType.NewArrayInit ?
                Ex.NewArrayInit(node.Type.GetElementType()!, args) :
                Ex.NewArrayBounds(node.Type.GetElementType()!, args), node.Expressions.ToArray());
    
    //parameter, runtimevariables: no changes

    /// <inheritdoc />
    protected override Expression VisitSwitch(SwitchExpression node) {
        //Largely the same structure as VisitConditional
        var cases = node.Cases.Select(VisitSwitchCase).ToArray();
        if (node.Type == typeof(void))
            return Linearize(cond => Ex.Switch(node.Type, cond, 
                Visit(node.DefaultBody), node.Comparison, cases), node.SwitchValue);
        
        var prm = Ex.Parameter(node.Type, $"$flatSwitch{counter++}");
        return Ex.Block(new[] {prm},
            Linearize(cond => Ex.Switch(typeof(void), cond,
                WithAssign(Visit(node.DefaultBody ?? throw new Exception(
                    "No default body for typed switch case")), prm), node.Comparison,
                cases.Select(c => Ex.SwitchCase(WithAssign(c.Body, prm), c.TestValues))
            ), node.SwitchValue),
            prm
        );
    }

    /// <inheritdoc />
    protected override Expression VisitTry(TryExpression node) {
        //Largely the same structure as VisitConditional
        if (node.Type == typeof(void))
            return base.VisitTry(node);
        
        var prm = Ex.Parameter(node.Type, $"$flatTry{counter++}");
        return Ex.Block(new[] {prm},
            Ex.MakeTry(typeof(void),
                WithAssign(Visit(node.Body), prm),
                Visit(node.Finally),
                null,
                node.Handlers.Select(h => {
                    var vh = VisitCatchBlock(h);
                    return Ex.MakeCatchBlock(h.Variable?.Type ?? typeof(Exception), 
                        h.Variable, WithAssign(vh.Body, prm), vh.Filter);
                })),
            prm
        );

    }

    /// <inheritdoc />
    protected override Expression VisitTypeBinary(TypeBinaryExpression node) =>
        Linearize(ex => node.NodeType == ExpressionType.TypeEqual ? 
            Ex.TypeEqual(ex, node.Type) :
            Ex.TypeIs(ex, node.Type), node.Expression);

    /// <inheritdoc />
    protected override Expression VisitUnary(UnaryExpression node) =>
        Linearize(ex => Ex.MakeUnary(node.NodeType, ex, node.Type), node.Operand);

}
}