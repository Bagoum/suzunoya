using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace BagoumLib.Expressions {
/// <summary>
/// Linearizes expressions.
/// <br/>A linearized expression is one of:
/// <br/>(1) an expression that can participate in basic C# operations (eg. can be the X in `return 2 + X;`),
/// <br/>(2) a BlockExpression whose component statements are linearized, and whose last statement satisfies (1),
/// <br/>(3) an expression with a return type of void (such as if/try/switch), whose children are linearized expressions.
/// <br/>Linearized expressions can be converted to source code without too much difficulty. See <see cref="PrintVisitor"/>.
/// </summary>
public class LinearizeVisitor : ExpressionVisitor {

    private int counter = 0;

    private Expression Linearize(Func<Expression[], Expression> combiner, params Expression?[] pieces) {
        Expression?[] linearized = pieces.Select(Visit).ToArray();
        if (!linearized.Any(ex => ex is BlockExpression))
            return combiner(linearized!);
        var prms = new List<ParameterExpression>();
        var stmts = new List<Expression>();
        var reduced_args = new Expression?[linearized.Length];
        linearized.ForEachI((i, ex) => {
            if (ex is BlockExpression bex) {
                prms.AddRange(bex.Variables);
                stmts.AddRange(bex.Expressions.Take(bex.Expressions.Count - 1));
                reduced_args[i] = bex.Expressions[bex.Expressions.Count - 1];
            } else {
                reduced_args[i] = ex;
            }
        });
        stmts.Add(combiner(reduced_args!));
        return Ex.Block(prms, stmts);
    }


    /// <summary>
    /// Invariant: expr is linearized.
    /// <br/>Returns a linearized expression equivalent to expr that, as a side effect,
    ///  assigns the value of expr to dst.
    /// </summary>
    private Ex WithAssign(Ex expr, ParameterExpression dst) {
        if (expr is BlockExpression bex) {
            var exprs = bex.Expressions.ToArray();
            //Don't need to assign if throwing
            if (exprs[exprs.Length - 1] is UnaryExpression {NodeType: ExpressionType.Throw})
                return expr;
            exprs[exprs.Length - 1] = Ex.Assign(dst, exprs[exprs.Length - 1]);
            return Ex.Block(bex.Variables, exprs);
        } else {
            if (expr is UnaryExpression {NodeType: ExpressionType.Throw})
                return expr;
            return Ex.Assign(dst, expr);
        }
    }

    protected override Expression VisitBinary(BinaryExpression node) =>
        Linearize(exs => Ex.MakeBinary(node.NodeType, exs[0], exs[1]), node.Left, node.Right);
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

    protected override CatchBlock VisitCatchBlock(CatchBlock node) {
        Expression? filter = null;
        if (node.Filter != null)
            filter = Visit(node.Filter);
        if (filter is BlockExpression)
            throw new Exception("Cannot have a block expression in catch filter");
        return Ex.MakeCatchBlock(node.Test, node.Variable, Visit(node.Body), filter);
    }

    protected override Expression VisitConditional(ConditionalExpression node) {
        if (node.Type == typeof(void))
            //If/then statements only require fixing the condition, since if statements can take blocks as children
            return Linearize(cond => Ex.Condition(cond[0], Visit(node.IfTrue), Visit(node.IfFalse), node.Type), node.Test);
        
        var ifT = Visit(node.IfTrue);
        var ifF = Visit(node.IfFalse);
        //Don't need to worry about loop/switch since the linearization invariant
        // guarantees that those expressions can never be typed
        if (ifF is BlockExpression || ifT is BlockExpression) {
            //This handling is more complex than the standard handling since it'd be incorrect
            // to just evaluate both branches and return the correct one.
            //Instead, we declare a variable outside an if statement, and write to it in the branches.
            var prm = Ex.Parameter(node.Type, $"flatTernary{counter++}");
            return Ex.Block(new[] {prm},
                Linearize(cond => Ex.Condition(cond[0], WithAssign(ifT, prm), WithAssign(ifF, prm), typeof(void)), node.Test),
                prm
            );
        } else
            return Linearize(cond => Ex.Condition(cond[0], ifT, ifF, node.Type), node.Test);
    }
    
    //Constant, default, dynamic, elementInit, extension, goto: no changes
    
    protected override Expression VisitIndex(IndexExpression node) =>
        Linearize(args => Ex.MakeIndex(args[0], node.Indexer, args.Skip(1)), 
            node.Arguments.Prepend(node.Object).ToArray());

    protected override Expression VisitInvocation(InvocationExpression node) =>
        Linearize(args => Ex.Invoke(args[0], args.Skip(1)), node.Arguments.Prepend(node.Expression).ToArray());
    
    //label, labeltarget, lambda: no changes

    protected override Expression VisitListInit(ListInitExpression node) {
        throw new Exception();
    }

    //loop: no changes. TODO: is it possible for loops to have values in C#?

    protected override Expression VisitMember(MemberExpression node) =>
        Linearize(args => Ex.MakeMemberAccess(args[0], node.Member), node.Expression);

    private (MemberBinding binding, BlockExpression? block) LinearizeMemberBinding(MemberBinding m) {
        if (m is MemberAssignment ma) {
            return Visit(ma.Expression) switch {
                BlockExpression bex => (Ex.Bind(ma.Member, bex.Expressions[bex.Expressions.Count - 1]), bex),
                { } ex => (Ex.Bind(ma.Member, ex), null)
            };
        }
        //membermemberbinding, memberlistbinding
        throw new Exception($"Member binding not handled: {m.BindingType}");
    }

    protected override Expression VisitMemberInit(MemberInitExpression node) {
        var newExpr = Visit(node.NewExpression);
        var bindings = node.Bindings.Select(LinearizeMemberBinding).ToArray();
        var prms = new List<ParameterExpression>();
        var stmts = new List<Expression>();
        Expression newExprLast = newExpr;
        if (newExpr is BlockExpression b) {
            prms.AddRange(b.Variables);
            stmts.AddRange(b.Expressions.Take(b.Expressions.Count - 1));
            newExprLast = b.Expressions[b.Expressions.Count - 1];
        }
        bindings.ForEachI((i, ex) => {
            if (ex.block != null) {
                prms.AddRange(ex.block.Variables);
                stmts.AddRange(ex.block.Expressions.Take(ex.block.Expressions.Count - 1));
            }
        });
        stmts.Add(Ex.MemberInit((newExprLast as NewExpression)!, bindings.Select(bd => bd.binding)));
        return Ex.Block(prms, stmts);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node) =>
        Linearize(args => Ex.Call(node.Object, node.Method, args), node.Arguments.ToArray());

    protected override Expression VisitNew(NewExpression node) =>
        Linearize(args => Ex.New(node.Constructor, args), node.Arguments.ToArray());

    protected override Expression VisitNewArray(NewArrayExpression node) =>
        Linearize(args => 
            node.NodeType == ExpressionType.NewArrayInit ?
                Ex.NewArrayInit(node.Type, args) :
                Ex.NewArrayBounds(node.Type, args), node.Expressions.ToArray());
    
    //parameter, runtimevariables: no changes

    protected override Expression VisitSwitch(SwitchExpression node) {
        //Largely the same structure as VisitConditional
        var cases = node.Cases.Select(VisitSwitchCase).ToArray();
        if (node.Type == typeof(void))
            return Linearize(cond => Ex.Switch(node.Type, cond[0], 
                Visit(node.DefaultBody), node.Comparison, cases), node.SwitchValue);
        
        var prm = Ex.Parameter(node.Type, $"flatSwitch{counter++}");
        return Ex.Block(new[] {prm},
            Linearize(cond => Ex.Switch(typeof(void), cond[0],
                WithAssign(Visit(node.DefaultBody), prm), node.Comparison,
                cases.Select(c => Ex.SwitchCase(WithAssign(c.Body, prm), c.TestValues))
            ), node.SwitchValue),
            prm
        );
    }

    protected override Expression VisitTry(TryExpression node) {
        //Largely the same structure as VisitConditional
        if (node.Type == typeof(void))
            return base.VisitTry(node);
        
        var prm = Ex.Parameter(node.Type, $"flatTry{counter++}");
        return Ex.Block(new[] {prm},
            Ex.MakeTry(typeof(void),
                WithAssign(Visit(node.Body), prm),
                Visit(node.Finally),
                null,
                node.Handlers.Select(h => {
                    var vh = VisitCatchBlock(h);
                    return Ex.MakeCatchBlock(h.Variable.Type, h.Variable, WithAssign(vh.Body, prm), vh.Filter);
                })),
            prm
        );

    }

    protected override Expression VisitTypeBinary(TypeBinaryExpression node) =>
        Linearize(ex => node.NodeType == ExpressionType.TypeEqual ? 
            Ex.TypeEqual(ex[0], node.Type) :
            Ex.TypeIs(ex[0], node.Type), node.Expression);

    protected override Expression VisitUnary(UnaryExpression node) =>
        Linearize(exs => Ex.MakeUnary(node.NodeType, exs[0], node.Type), node.Operand);

}
}