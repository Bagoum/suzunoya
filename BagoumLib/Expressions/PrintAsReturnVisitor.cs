using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace BagoumLib.Expressions {
/// <summary>
/// A visitor that transforms the provided expression with an implicit return into code with an explicit return.
/// </summary>
public class PrintAsReturnVisitor : DerivativePrintVisitor {
    public PrintAsReturnVisitor(PrintVisitor parent) : base(parent) { }

    public override Expression Visit(Expression node) {
        if (node.Type == typeof(void))
            return parent.Visit(node);
        switch (node) {
            case LoopExpression:
            case SwitchExpression:
            case TryExpression:
                //Loops, switches, and tries are always linearized under (3), and thus cannot have implicit returns
                throw new Exception("Cannot return a loop/switch/try. Is this expression linearized?");
            case (BlockExpression bx):
                return VisitBlock(bx);
            case LabelExpression:
                throw new Exception("Cannot return a label.");
            case UnaryExpression ue:
                //Throw statements are their own returns
                if (ue.NodeType == ExpressionType.Throw)
                    return Stmter.Visit(node);
                break;
        }
        Add("return ");
        parent.Visit(node);
        Add(PrintToken.semicolon, PrintToken.newline);
        return node;
    }

    protected override Expression VisitBlock(BlockExpression node) {
        var TypeAssigns = new Dictionary<Expression, ParameterExpression>();
        var consumes = new EnumerateVisitor();
        var inspectExprs = node.Expressions.Take(node.Expressions.Count - 1).ToArray();
        foreach (var prm in node.Variables) {   
            foreach (var expr in inspectExprs) {
                if (expr is BinaryExpression {NodeType: ExpressionType.Assign} be && be.Left == prm) {
                    if (consumes.Enumerate(be.Right).Any(e => e == prm))
                        break;
                    TypeAssigns[expr] = prm;
                    goto prmDone;
                }
                if (consumes.Enumerate(expr).Any(e => e == prm))
                    break;
            }
            parent.VisitTypedParameter(prm);
            Add(PrintToken.semicolon, PrintToken.newline);
            prmDone: ;
        }
        foreach (var stmt in inspectExprs) {
            if (TypeAssigns.TryGetValue(stmt, out var prm))
                Add(new PrintToken.TypeName(prm.Type), " ");
            Stmter.Visit(stmt);
        }
        Visit(node.Expressions[node.Expressions.Count - 1]);
        return node;
    }
}
}