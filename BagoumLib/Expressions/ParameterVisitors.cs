using System.Collections.Generic;
using System.Linq.Expressions;

namespace BagoumLib.Expressions;

/// <summary>
/// An expression visitor that replaces a parameter with an arbitrary expression.
/// <br/>The resulting tree may not be sound if the expression is of a different type
///  or the parameter is written to and the expression is not writeable.
/// </summary>
public class ReplaceParameterVisitor(ParameterExpression source, Expression replaceWith) : ExpressionVisitor {
    /// <inheritdoc/>
    protected override Expression VisitParameter(ParameterExpression node) {
        if (node == source)
            return replaceWith;
        return base.VisitParameter(node);
    }
}

/// <summary>
/// Checks if a expression tree has any parameters not bound in a Block.
/// </summary>
public class HasUnboundParameterVisitor : ExpressionVisitor {
    public readonly List<ParameterExpression> UnboundParameters = new();
    private readonly HashSet<ParameterExpression> blockParameters = new();

    /// <inheritdoc/>
    protected override Expression VisitParameter(ParameterExpression node) {
        if (!blockParameters.Contains(node))
            UnboundParameters.Add(node);
        return base.VisitParameter(node);
    }

    /// <inheritdoc/>
    protected override Expression VisitBlock(BlockExpression node) {
        foreach (var p in node.Variables)
            blockParameters.Add(p);
        var b = base.VisitBlock(node);
        foreach (var p in node.Variables)
            blockParameters.Remove(p);
        return b;
    }
}