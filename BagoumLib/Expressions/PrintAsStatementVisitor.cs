﻿using System;
using System.Linq.Expressions;
using JetBrains.Annotations;
using static BagoumLib.Expressions.PrintToken;

namespace BagoumLib.Expressions {
public class PrintAsStatementVisitor : DerivativePrintVisitor {
    public override PrintAsStatementVisitor Stmter => this;
    public PrintAsStatementVisitor(PrintVisitor parent) : base(parent) { }

    public override Expression? Visit(Expression? node) {
        if (node == null) return node;
        switch (node) {
            case BlockExpression:
                return parent.Visit(node);
            case LoopExpression:
            case SwitchExpression:
            case TryExpression:
                parent.Visit(node);
                Add(newline);
                return node;
            case ConditionalExpression ce:
                if (ce.Type == typeof(void)) {
                    parent.Visit(node);
                    Add(newline);
                    return node;
                }
                break;
        }
        //This can get autogenerated sometimes
        if (node is DefaultExpression d && d.Type == typeof(void))
            return node;
        //Labels are also printed with semicolons. This avoids end-of-function labels.
        parent.Visit(node);
        Add(semicolon, newline);
        return node;
    }
}
}