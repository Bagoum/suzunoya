using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Functional;
using Ex = System.Linq.Expressions.Expression;
using static BagoumLib.Expressions.PrintToken;
using static BagoumLib.Expressions.VisitorHelpers;

namespace BagoumLib.Expressions {
/// <summary>
/// A data representation of elements that can be processed to print an <see cref="Expression"/>.
/// </summary>
public abstract record PrintToken {
    /// <summary>
    /// A parameter.
    /// </summary>
    public record Parameter(ParameterExpression ex) : PrintToken;

    /// <summary>
    /// Any constant value.
    /// </summary>
    public record Constant(object? value) : PrintToken;

    /// <summary>
    /// The name of a type.
    /// </summary>
    public record TypeName(Type t) : PrintToken;

    /// <summary>
    /// A label (used for gotos).
    /// </summary>
    public record Label(LabelTarget label) : PrintToken;

    /// <summary>
    /// A newline, which will also include any indents.
    /// </summary>
    public record Newline : PrintToken;

    /// <summary>
    /// If the previous token is a newline, then cancel it out.
    /// </summary>
    public record UndoNewline : PrintToken;

    /// <summary>
    /// Add an indent to the printer.
    /// </summary>
    public record Indent : PrintToken;

    /// <summary>
    /// Remove an indent from the printer.
    /// </summary>
    public record Dedent : PrintToken;

    /// <summary>
    /// A semicolon.
    /// </summary>
    public record Semicolon : PrintToken;

    /// <summary>
    /// Any plain text to be printed.
    /// <br/>Do not put newlines/indents/dedents here, use the dedicated tokens instead.
    /// </summary>
    public record Text(string String) : PrintToken;

    /// <summary>
    /// Make a <see cref="Text"/> token from a string.
    /// </summary>
    public static implicit operator PrintToken(string s) => new Text(s);
    /// <summary>
    /// <see cref="Newline"/> singleton
    /// </summary>
    public static readonly PrintToken newline = new Newline();
    /// <summary>
    /// <see cref="UndoNewline"/> singleton
    /// </summary>
    public static readonly PrintToken undoNewline = new UndoNewline();
    /// <summary>
    /// <see cref="Indent"/> singleton
    /// </summary>
    public static readonly PrintToken indent = new Indent();
    /// <summary>
    /// <see cref="Dedent"/> singleton
    /// </summary>
    public static readonly PrintToken dedent = new Dedent();
    /// <summary>
    /// <see cref="Semicolon"/> singleton
    /// </summary>
    public static readonly PrintToken semicolon = new Semicolon();
}

/// <summary>
/// Abstract base class for expression visitors that convert a tree into a list of PrintTokens
/// </summary>
public abstract class PrintVisitorAbs : ExpressionVisitor {
    /// <summary>
    /// Accumulated tokens from visiting a tree
    /// </summary>
    public virtual List<PrintToken> Tokens { get; } = new();
    /// <summary>
    /// Sub-printer responsible for printing parenthesized expressions
    /// </summary>
    public abstract PrintAsParenthesizedVisitor Parener { get; }
    /// <summary>
    /// Sub-printer responsible for printing expressions as statements
    /// </summary>
    public abstract PrintAsStatementVisitor Stmter { get; }
    /// <summary>
    /// Sub-printer responsible for printing expressions as return statements
    /// </summary>
    public abstract PrintAsReturnVisitor Returner { get; }
    /// <summary>
    /// Add a token to be printed.
    /// </summary>
    protected void Add(PrintToken p) => Tokens.Add(p);
    /// <summary>
    /// Add many tokens to be printed.
    /// </summary>
    protected void Add(params PrintToken[] p) => Tokens.AddRange(p);
}

/// <summary>
/// A printer that applies some modifications to a base printer.
/// </summary>
public abstract class DerivativePrintVisitor : PrintVisitorAbs {
    /// <summary>
    /// Base printer
    /// </summary>
    protected readonly PrintVisitor parent;
    /// <inheritdoc />
    public override List<PrintToken> Tokens => parent.Tokens;
    /// <inheritdoc />
    public override PrintAsParenthesizedVisitor Parener => parent.Parener;
    /// <inheritdoc />
    public override PrintAsStatementVisitor Stmter => parent.Stmter;
    /// <inheritdoc />
    public override PrintAsReturnVisitor Returner => parent.Returner;

    /// <summary>
    /// Create a printer from the given base printer.
    /// </summary>
    public DerivativePrintVisitor(PrintVisitor parent) {
        this.parent = parent;
    }
}

/// <summary>
/// Converts an expression into an array of <see cref="PrintToken"/>s that can be trivially printed.
/// <br/>If the expression is linearized, then the output of this will construct valid C# source code,
/// given a sufficiently apt type and object printer (see <see cref="ExpressionPrinter"/>).
/// </summary>
public class PrintVisitor : PrintVisitorAbs {
    /// <inheritdoc />
    public override PrintAsParenthesizedVisitor Parener { get; }
    /// <inheritdoc />
    public override PrintAsStatementVisitor Stmter { get; }
    /// <inheritdoc />
    public override PrintAsReturnVisitor Returner { get; }

    /// <summary>
    /// Create a printer.
    /// </summary>
    public PrintVisitor() {
        Parener = new PrintAsParenthesizedVisitor(this);
        Stmter = new PrintAsStatementVisitor(this);
        Returner = new PrintAsReturnVisitor(this);
    }

    /// <summary>
    /// Visit an expression and convert it to tokens, stored in <see cref="PrintVisitorAbs.Tokens"/>
    /// </summary>
    public PrintToken[] Print(Ex ex) {
        Tokens.Clear();
        Visit(ex);
        return Tokens.ToArray();
    }

    /// <inheritdoc />
    public override Expression? Visit(Expression? node) {
        if (node == null) return null;
        if (node.NodeType.IsChecked()) {
            Add("checked(");
            base.Visit(node);
            Add(")");
            return node;
        } else
            return base.Visit(node);
    }

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node) {
        Parener.Visit(node.Left);
        if (node.NodeType == ExpressionType.ArrayIndex) {
            Add("[");
            Visit(node.Right);
            Add("]");
        } else {
            Add($" {BinaryOperatorString(node.NodeType)} ");
            Parener.Visit(node.Right);
        }
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitBlock(BlockExpression node) {
        var TypeAssigns = new Dictionary<Expression, ParameterExpression>();
        var consumes = new EnumerateVisitor();
        foreach (var prm in node.Variables) {
            //If there are no right-hand assignments of PRM preceding the first left-hand assignment,
            // then we can join its declaration with its first left-hand assignment.
            // eg. int x; int y; y = (x = 0); x = 5; //Can join Y but not X -> int x; int y = (x = 0); x = 5;
            //     int x; int y; x = 5; y = (x = 0); //Can join both X and Y -> int x = 5; int y = (x = 0);
            foreach (var expr in node.Expressions) {
                if (expr is BinaryExpression {NodeType: ExpressionType.Assign} be && be.Left == prm) {
                    if (consumes.Enumerate(be.Right).Any(e => e == prm))
                        break;
                    TypeAssigns[expr] = prm;
                    goto prmDone;
                }
                if (consumes.Enumerate(expr).Any(e => e == prm))
                    break;
            }
            VisitTypedParameter(prm);
            Add(semicolon, newline);
            prmDone: ;
        }
        foreach (var stmt in node.Expressions) {
            if (TypeAssigns.TryGetValue(stmt, out var prm))
                Add(new TypeName(prm.Type), " ");
            Stmter.Visit(stmt);
        }
        return node;
    }

    private CatchBlock VisitCatchBlock(CatchBlock node, PrintVisitorAbs innerVisitor) {
        Add(" catch");
        if (node.Variable != null) {
            Add(" (");
            VisitTypedParameter(node.Variable);
            Add(")");
        }
        if (node.Filter != null) {
            Add(" when (");
            Visit(node.Filter);
            Add(")");
        }
        Add(" {", indent, newline);
        innerVisitor.Visit(node.Body);
        Add(undoNewline, dedent, newline, "}");
        return node;
    }
    
    public Expression VisitConditionalAsIfElse(ConditionalExpression node) {
        Add("if (");
        Visit(node.Test);
        Add(") {", indent, newline);
        Stmter.Visit(node.IfTrue);
        Add(undoNewline, dedent, newline, "}");
        if (node.IfFalse is not DefaultExpression) {
            Add(" else {", indent, newline);
            Stmter.Visit(node.IfFalse);
            Add(undoNewline, dedent, newline, "}");
        }
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitConditional(ConditionalExpression node) {
        if (node.Type == typeof(void)) {
            //if/else handling
            return VisitConditionalAsIfElse(node);
        } else {
            //ternary handling
            Parener.Visit(node.Test);
            Add(" ?", indent, newline);
            Parener.Visit(node.IfTrue);
            Add(" :", newline);
            Parener.Visit(node.IfFalse);
            Add(dedent);
            return node;
        }
    }

    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node) {
        Add(new Constant(node.Value));
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitDefault(DefaultExpression node) {
        //default(void) can get autogenerated sometimes at the end of void-typed blocks.
        if (node.Type != typeof(void))
            Add("default(", new TypeName(node.Type), ")");
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitDynamic(DynamicExpression node) {
        throw new Exception();
    }

    /// <inheritdoc />
    protected override ElementInit VisitElementInit(ElementInit node) {
        throw new Exception();
    }

    /// <inheritdoc />
    protected override Expression VisitExtension(Expression node) {
        throw new Exception();
    }

    /// <inheritdoc />
    protected override Expression VisitGoto(GotoExpression node) {
        if (node.Kind == GotoExpressionKind.Continue)
            Add("continue");
        else if (node.Kind == GotoExpressionKind.Break)
            Add("break");
        else if (node.Kind == GotoExpressionKind.Return) {
            Returner.Visit(node.Value);
        } else
            Add("goto ", new Label(node.Target));
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitIndex(IndexExpression node) {
        Parener.Visit(node.Object);
        Add("[");
        Visit(node.Arguments[0]);
        for (int ii = 1; ii < node.Arguments.Count; ++ii) {
            Add(", ");
            Visit(node.Arguments[ii]);
        }
        Add("]");
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitInvocation(InvocationExpression node) {
        Parener.Visit(node.Expression);
        VisitArguments(node.Arguments, node.Expression.Type.GetMethod("Invoke")!.GetParameters());
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitLabel(LabelExpression node) {
        Add(new Label(node.Target), ":");
        return node;
    }

    /// <inheritdoc />
    protected override LabelTarget VisitLabelTarget(LabelTarget? node) {
        throw new Exception();
    }

    /// <inheritdoc />
    protected override Expression VisitLambda<T>(Expression<T> node) {
        Add("(", new TypeName(typeof(T)), ") ((");
        var method = node.Type.GetMethod("Invoke")!;
        var prms = method.GetParameters();
        for (int ii = 0; ii < prms.Length; ++ii) {
            if (ii > 0)
                Add(", ");
            if (ParameterByRefPrefix(prms[ii]).Try(out var pref))
                Add(pref, " ");
            VisitTypedParameter(node.Parameters[ii]);
        }
        if (!node.Body.IsBlockishExpression()) {
            Add(") => ");
            Visit(node.Body);
            Add(")");
        } else {
            Add(") => {", indent, newline);
            if (method.ReturnType == typeof(void))
                Stmter.Visit(node.Body);
            else
                Returner.Visit(node.Body);
            Add(undoNewline, dedent, newline, "})");
        }
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitListInit(ListInitExpression node) {
        throw new Exception();
    }

    /// <inheritdoc />
    protected override Expression VisitLoop(LoopExpression node) {
        Add("while (true) {", indent, newline);
        //Don't use keywords since nested loops can have cross-loop behavior with labels
        if (node.ContinueLabel != null)
            Add(new Label(node.ContinueLabel), ":;", newline);
        Stmter.Visit(node.Body);
        Add(undoNewline, dedent, newline, "}");
        if (node.BreakLabel != null)
            Add(newline, new Label(node.BreakLabel), ":;");
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node) {
        if (node.Expression != null) {
            Parener.Visit(node.Expression);
        } else
            Add(new TypeName(node.Member.DeclaringType!));
        Add(".", node.Member.Name);
        return node;
    }

    /// <inheritdoc />
    protected override MemberAssignment VisitMemberAssignment(MemberAssignment node) {
        Add($"{node.Member.Name} =");
        Visit(node.Expression);
        return node;
    }

    //This delegates to the other methods
    /// <inheritdoc />
    protected override MemberBinding VisitMemberBinding(MemberBinding node) {
        return base.VisitMemberBinding(node);
    }

    /// <inheritdoc />
    protected override Expression VisitMemberInit(MemberInitExpression node) {
        Visit(node.NewExpression);
        Add("{", indent);
        for (int ii = 0; ii < node.Bindings.Count; ++ii) {
            if (ii > 0)
                Add(", ");
            VisitMemberBinding(node.Bindings[ii]);
        }
        Add(dedent, "}");
        return node;
    }

    /// <inheritdoc />
    protected override MemberListBinding VisitMemberListBinding(MemberListBinding node) {
        throw new Exception();
    }

    /// <inheritdoc />
    protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding node) {
        throw new Exception();
    }

    /// <summary>
    /// Print an argument list.
    /// </summary>
    /// <param name="args">Arguments to print</param>
    /// <param name="asParams">True if the argument types should be included</param>
    /// <param name="parens">True if should be wrapped in parentheses</param>
    protected void VisitArguments(IReadOnlyList<Expression> args, ParameterInfo[]? asParams, bool parens=true) {
        if (parens)
            Add("(");
        for (int ii = 0; ii < args.Count; ++ii) {
            if (ii > 0)
                Add(", ");
            if (asParams != null && ParameterByRefPrefix(asParams[ii]).Try(out var pref))
                Add(pref, " ");
            Visit(args[ii]);
        }
        if (parens)
            Add(")");
    }

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node) {
        if (node.Object != null) {
            Parener.Visit(node.Object);
        } else {
            if (node.Method.DeclaringType != null)
                Add(new TypeName(node.Method.DeclaringType));
        }
        Add($".{node.Method.Name}");
        if (node.Method.IsGenericMethod) {
            Add("<");
            var gargs = node.Method.GetGenericArguments();
            foreach (var (i, t) in gargs.Enumerate()) {
                if (i > 0) Add(",");
                Add(new TypeName(t));
            }
            Add(">");
        }
        VisitArguments(node.Arguments, node.Method.GetParameters());
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitNew(NewExpression node) {
        if (node.Type.IsConstructedGenericType &&
            CSharpTypePrinter.tupleTypes.Contains(node.Type.GetGenericTypeDefinition())) {
            VisitArguments(node.Arguments, null);
        } else {
            Add("new ", new TypeName(node.Type));
            VisitArguments(node.Arguments, 
                (node.Constructor ?? throw new Exception("No constructor provided")).GetParameters());
        }
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitNewArray(NewArrayExpression node) {
        if (node.NodeType == ExpressionType.NewArrayInit) {
            Add("new ", new TypeName(node.Type.GetElementType()!), "[] {", indent);
            VisitArguments(node.Expressions, null, false);
            Add(dedent, "}");
        } else {
            Add("new ", new TypeName(node.Type.GetElementType()!), "[");
            VisitArguments(node.Expressions, null, false);
            Add("]");
        }
        return node;
    }
    
    /// <summary>
    /// Display a parameter and its type, eg. as 'int myInt'.
    /// </summary>
    public Expression VisitTypedParameter(ParameterExpression p) {
        Add(new TypeName(p.Type), " ");
        return VisitParameter(p);
    }

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node) {
        Add(new Parameter(node));
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node) {
        throw new Exception();
    }

    /// <inheritdoc />
    protected override Expression VisitSwitch(SwitchExpression node) {
        Add("switch (");
        Visit(node.SwitchValue);
        Add(") {", indent, newline);
        var visitor = node.Type == typeof(void) ? Stmter : (PrintVisitorAbs)Returner;
        foreach (var cas in node.Cases)
            VisitSwitchCase(cas, visitor);
        Add("default:", indent, newline);
        visitor.Visit(node.DefaultBody);
        Add("break;", dedent, dedent, newline, "}");
        return node;
    }

    private SwitchCase VisitSwitchCase(SwitchCase node, PrintVisitorAbs innerVisitor) {
        for (int ii = 0; ii < node.TestValues.Count; ++ii) {
            if (ii > 0)
                Add(newline);
            Add("case (");
            Visit(node.TestValues[ii]);
            Add("):");
        }
        Add(indent, newline);
        innerVisitor.Visit(node.Body);
        Add("break;", dedent, newline);
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitTry(TryExpression node) {
        if (node.Fault != null)
            throw new Exception("Cannot handle 'fault' keyword in C#");
        Add("try {", indent, newline);
        var visitor = node.Type == typeof(void) ? Stmter : (PrintVisitorAbs)Returner;
        visitor.Visit(node.Body);
        Add(undoNewline, dedent, newline, "}");
        foreach (var catcher in node.Handlers)
            VisitCatchBlock(catcher, visitor);
        if (node.Finally != null) {
            Add(" finally {", indent, newline);
            Stmter.Visit(node.Finally);
            Add(undoNewline, dedent, newline, "}");
        }
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitTypeBinary(TypeBinaryExpression node) {
        void AddInner() {
            Parener.Visit(node.Expression);
        }
        if (node.NodeType == ExpressionType.TypeIs) {
            AddInner();
            Add(" is ",
                new TypeName(node.Type));
        } else {
            throw new Exception($"Type expr not handled: {node.NodeType}");
        }
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitUnary(UnaryExpression node) {
        if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.ConvertChecked) {
            Add("(",
                new TypeName(node.Type),
                ")");
            Parener.Visit(node.Operand);
        } else if (node.NodeType == ExpressionType.TypeAs) {
            Parener.Visit(node.Operand);
            Add(" as ",
                new TypeName(node.Type));
        } else {
            var opStr = UnaryOperatorString(node.NodeType, node.Operand.Type);
            if (opStr.IsLeft)
                Add(opStr.Left);
            Parener.Visit(node.Operand);
            if (!opStr.IsLeft)
                Add(opStr.Right);
        }
        return node;
    }
    
}
}