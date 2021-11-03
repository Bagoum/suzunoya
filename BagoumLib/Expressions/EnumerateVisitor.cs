using System.Collections.Generic;
using System.Linq.Expressions;

namespace BagoumLib.Expressions {
public class EnumerateVisitor : ExpressionVisitor {
    private List<Expression> exprs = null!;

    public List<Expression> Enumerate(Expression root) {
        exprs = new List<Expression>();
        Visit(root);
        return exprs;
    }

    public override Expression Visit(Expression node) {
        if (node != null)
            exprs.Add(node);
        return base.Visit(node);
    }
}
}