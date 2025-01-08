using LanguageServer.VsCode.Contracts;
using Scriptor.Reflection;

namespace Scriptor.Analysis;

/// <summary>
/// Helpers for semantic token types.
/// </summary>
public static class SemanticTokenTypes {
    /// <summary>
    /// A type, eg. `int.`
    /// </summary>
    public const string Type = "type";
    /// <summary>
    /// A class method.
    /// </summary>
    public const string Method = "method";
    /// <summary>
    /// A function.
    /// </summary>
    public const string Function = "function";
    /// <summary>
    /// An operator, eg. `+`.
    /// </summary>
    public const string Operator = "dmkOperator";
    /// <summary>
    /// A parameter/argument.
    /// </summary>
    public const string Parameter = "parameter";
    /// <summary>
    /// A variable.
    /// </summary>
    public const string Variable = "variable";
    /// <summary>
    /// An enum member.
    /// </summary>
    public const string EnumMember = "dmkEnumMember";
    /// <summary>
    /// A keyword, eg. `var`.
    /// </summary>
    public const string Keyword = "keyword";
    /// <summary>
    /// A string, eg. `"hello world"`.
    /// </summary>
    public const string String = "string";
    /// <summary>
    /// A number, eg. `3.0`.
    /// </summary>
    public const string Number = "number";

    /// <summary>
    /// All valid semantic token types.
    /// </summary>
    public static readonly string[] Values = [
        Type, Method, Function, Operator, Parameter,
        Variable, EnumMember, Keyword, String, Number
    ];
    
    /// <summary>
    /// Get the semantic token type for a method.
    /// </summary>
    public static string MethodType(IMethodSignature mi) {
        if (mi.GetAttribute<OperatorAttribute>() != null || mi.GetAttribute<BDSL2OperatorAttribute>() != null)
            return SemanticTokenTypes.Operator;
        if (mi.Member.Symbol() == SymbolKind.Enum)
            return SemanticTokenTypes.EnumMember;
        return SemanticTokenTypes.Method;
    }
}

/// <summary>
/// Basic universal semantic token modifiers.
/// </summary>
public static class BasicSemanticTokenModifiers { 
    /// <summary>
    /// A constant variable.
    /// </summary>
    public const string Const = "const";
    /// <summary>
    /// A dynamic-lookup variable.
    /// </summary>
    public const string DynamicVar = "dmkdynamicvar";
}