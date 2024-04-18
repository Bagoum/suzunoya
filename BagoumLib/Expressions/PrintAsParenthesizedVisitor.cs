using System;
using System.Linq.Expressions;
using JetBrains.Annotations;

namespace BagoumLib.Expressions {
/// <summary>
/// A visitor that transforms the provided expression into a parenthesized expression (only if necessary).
/// </summary>
public class PrintAsParenthesizedVisitor : DerivativePrintVisitor {
    /// <inheritdoc />
    public override PrintAsParenthesizedVisitor Parener => this;
    /// <summary>
    /// Create a <see cref="PrintAsParenthesizedVisitor"/>
    /// </summary>
    public PrintAsParenthesizedVisitor(PrintVisitor parent) : base(parent) { }

    private static readonly Type tvoid = typeof(void);

    /// <inheritdoc />
    public override Expression? Visit(Expression? node) {
        if (node == null) return node;
        switch (node) {
            case ConstantExpression:
            case DefaultExpression:
            case IndexExpression:
            case InvocationExpression:
            case MemberExpression:
            case MethodCallExpression:
            case NewExpression:
            case NewArrayExpression:
            case ParameterExpression:
            case UnaryExpression { NodeType: ExpressionType.Throw }:
                return parent.Visit(node);
            case BlockExpression:
            case LoopExpression:
            case SwitchExpression:
            case TryExpression:
                throw new Exception($"Cannot parenthesize exprs of type {node.GetType()}." +
                                    $"You may need to linearize this expression tree.");
            default:
                Add("(");
                parent.Visit(node);
                Add(")");
                return node;
        }
    }
}
}