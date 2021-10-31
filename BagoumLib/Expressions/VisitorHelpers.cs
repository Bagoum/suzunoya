using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using BagoumLib.Functional;

namespace BagoumLib.Expressions {

public static class VisitorHelpers {
    
    public static Expression Linearize(this Expression ex) => new LinearizeVisitor().Visit(ex);

    public static string? ParameterByRefPrefix(ParameterInfo p) => p.ParameterType.IsByRef ?
        p.IsIn ? "in" :
        p.IsOut ? "out" : "ref" :
        null;
    private static string FirstUpper(string s) {
        if (s.Length == 0 || char.IsUpper(s[0])) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }
    private static string FirstLower(string s) {
        if (s.Length == 0 || char.IsLower(s[0])) return s;
        return char.ToLower(s[0]) + s.Substring(1);
    }

    /// <summary>
    /// Converts a type into a camel-case representation with no symbols.
    /// </summary>
    public static string NameTypeInWords(Type t) {
        if (CSharpTypePrinter.SimpleTypeNameMap.TryGetValue(t, out var v))
            return v;
        if (t.IsArray)
            return $"{NameTypeInWords(t.GetElementType()!)}Array";
        if (t.IsConstructedGenericType)
            return $"{NameTypeInWords(t.GetGenericTypeDefinition())}Of" +
                   $"{string.Join("And", t.GenericTypeArguments.Select(NameTypeInWords).Select(FirstUpper))}";
        if (t.IsGenericType) {
            int cutFrom = t.Name.IndexOf('`');
            if (cutFrom > 0)
                return FirstLower(t.Name.Substring(0, cutFrom));
        }
        return FirstLower(t.Name);
    }

    private static readonly HashSet<ExpressionType> CheckedTypes = new() {
        ExpressionType.AddChecked, ExpressionType.AddAssignChecked, ExpressionType.ConvertChecked,
        ExpressionType.MultiplyChecked, ExpressionType.MultiplyAssignChecked, ExpressionType.NegateChecked,
        ExpressionType.SubtractChecked, ExpressionType.SubtractAssignChecked
    };
    public static bool IsChecked(ExpressionType e) => CheckedTypes.Contains(e);
    

    private static Either<string, string> Left(string s) => new(true, s, null!);
    private static Either<string, string> Right(string s) => new(false, null!, s);
    
    public static string BinaryOperatorString(ExpressionType e) => e switch {
        ExpressionType.Add => "+",
        ExpressionType.AddAssign => "+=",
        ExpressionType.AddAssignChecked => "+=",
        ExpressionType.AddChecked => "+",
        ExpressionType.And => "&",
        ExpressionType.AndAlso => "&&",
        ExpressionType.AndAssign => "&=",
        ExpressionType.Assign => "=",
        ExpressionType.Coalesce => "??",
        ExpressionType.Divide => "/",
        ExpressionType.DivideAssign => "/=",
        ExpressionType.Equal => "==",
        ExpressionType.ExclusiveOr => "^",
        ExpressionType.ExclusiveOrAssign => "^=",
        ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LeftShift => "<<",
        ExpressionType.LeftShiftAssign => "<<=",
        ExpressionType.LessThan => "<",
        ExpressionType.LessThanOrEqual => "<=",
        ExpressionType.Modulo => "%",
        ExpressionType.ModuloAssign => "%=",
        ExpressionType.Multiply => "*",
        ExpressionType.MultiplyAssign => "*=",
        ExpressionType.MultiplyChecked => "*",
        ExpressionType.MultiplyAssignChecked => "*=",
        ExpressionType.NotEqual => "!=",
        ExpressionType.Or => "|",
        ExpressionType.OrAssign => "|=",
        ExpressionType.OrElse => "||",
        ExpressionType.RightShift => ">>",
        ExpressionType.RightShiftAssign => ">>=",
        ExpressionType.Subtract => "-",
        ExpressionType.SubtractAssign => "-=",
        ExpressionType.SubtractChecked => "-",
        ExpressionType.SubtractAssignChecked => "-=",
        
        _ => throw new Exception($"{e} is not a handled binary operator")
    };

    public static Either<string, string> UnaryOperatorString(ExpressionType e, Type operand) => e switch {
        ExpressionType.ArrayLength => Right(".Length"),
        ExpressionType.Decrement => Right(" - 1"),
        ExpressionType.Increment => Right(" + 1"),
        ExpressionType.IsFalse => Left("!"), //TODO??
        ExpressionType.IsTrue => Left(""), //TODO??
        ExpressionType.Negate => Left("-"),
        ExpressionType.NegateChecked => Left("-"),
        ExpressionType.Not => Left(operand == typeof(bool) ? "!" : "~"),
        ExpressionType.OnesComplement => Left("~"),
        ExpressionType.PostDecrementAssign => Right("--"),
        ExpressionType.PostIncrementAssign => Right("++"),
        ExpressionType.PreDecrementAssign => Left("--"),
        ExpressionType.PreIncrementAssign => Left("--"),
        ExpressionType.Throw => Left("throw "),
        ExpressionType.UnaryPlus => Left("+"),
        
        _ => throw new Exception($"{e} is not a handled unary operator")
    };
    //Not handled: Block, Call, Conditional, Constant, DebugInfo, Default, Dynamic, Extension (?),
    // Goto, Index, Invoke, Label, Lambda, ListInit, Loop, MemberAccess, MemberInit,
    // New, NewArrayBounds, NewArrayInit, RuntimeVariables, Switch, Try, TypeEqual, 
    //Explicitly handled: ArrayIndex, Convert/Checked, Not, TypeAs, TypeIs
    //Quote is technically unary but idk what it does
    //Not in C#: Power, PowerAssign, Unbox?
}
}