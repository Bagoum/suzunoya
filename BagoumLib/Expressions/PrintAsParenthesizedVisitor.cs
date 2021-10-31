using System;
using System.Linq.Expressions;
using JetBrains.Annotations;

namespace BagoumLib.Expressions {
public class PrintAsParenthesizedVisitor : DerivativePrintVisitor {
    public override PrintAsParenthesizedVisitor Parener => this;
    public PrintAsParenthesizedVisitor([NotNull] PrintVisitor parent) : base(parent) { }

    private static readonly Type tvoid = typeof(void);
    public override Expression Visit(Expression node) {
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