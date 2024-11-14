using System;
using System.Runtime.CompilerServices;
using BagoumLib;
using JetBrains.Annotations;

namespace Scriptor;

/// <summary>
/// A reflection method that takes expressions/expression functions as arguments (such as ExTP)
///  and returns something that is not expression based (such as TP or RootedVTP).
/// <br/>If this occurs alongside <see cref="FallthroughAttribute"/>,
///  then it is treated as an expression compiler in BDSL1.
/// <br/>Replaces ExprCompilerAttribute.
/// </summary>
public class ExpressionBoundaryAttribute : Attribute { }


/// <summary>
/// Attribute marking that the `typeIndex`th generic variable in this method should only be allowed
///  to take on one of the provided `possibleTypes`.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class RestrictTypesAttribute : Attribute {
    public readonly int typeIndex;
    public readonly Type[] possibleTypes;

    /// <summary></summary>
    public RestrictTypesAttribute(int typeIndex, params Type[] possibleTypes) {
        this.typeIndex = typeIndex;
        this.possibleTypes = possibleTypes;
    }
}


public class FileLinkAttribute : Attribute {
    public readonly string file;
    public readonly string member;
    public readonly int line;

    /// <summary></summary>
    public FileLinkAttribute([CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0) {
        this.file = file;
        this.member = member;
        this.line = line;
    }
    
    public string FileLink(string? content = null) => Logging.ToFileLink(file, line, content);
}

/// <summary>
/// Mark that this function (or all methods in this class) can be converted into a constant expression in BDSL2 reflection.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class ConstableAttribute : Attribute {
    public readonly bool constableInAOT;

    /// <summary></summary>
    public ConstableAttribute(bool constableInAOT = true) {
        this.constableInAOT = constableInAOT;
    }
}

[AttributeUsage(AttributeTargets.All)]
public class DontReflectAttribute : Attribute { }
/// <summary>
/// Attribute marking a reflection alias for this method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple=true)]
public class AliasAttribute : Attribute {
    public readonly string alias;

    /// <summary></summary>
    public AliasAttribute(string alias) {
        this.alias = alias;
    }
}

/// <summary>
/// On an assembly, Reflect marks that classes in the assembly should be examined for possible reflection.
/// <br/>If the assembly is marked, then on a class, Reflect marks that the methods in the class should be reflected.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class)]
[MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
public class ReflectAttribute : FileLinkAttribute {
    public readonly Type? returnType;
    /// <summary></summary>
    public ReflectAttribute(Type? returnType = null, 
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        // ReSharper disable ExplicitCallerInfoArgument
        [CallerLineNumber] int line = 0) : base(file, member, line) {
        // ReSharper restore ExplicitCallerInfoArgument
        this.returnType = returnType;
    }
}



/// <summary>
/// Attribute marking a reflection method that is automatically applied if none other match.
/// </summary>
[AttributeUsage((AttributeTargets.Method))]
public class FallthroughAttribute : Attribute {
    //TODO you probably don't need priority anymore
    public readonly int priority;

    /// <summary></summary>
    public FallthroughAttribute(int priority=0) {
        this.priority = priority;
    }
}

/// <summary>
/// (BDSL2) The marked method has a lexical scope governing its arguments.
/// <br/>It may have automatically-defined variables, as defined by AutoVarMethod in provider code.
/// <br/>If any of the arguments are GenCtxProperty or GenCtxProperties, then the compiler
///  will assign the lexical scope to the constructed properties.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
public class CreatesInternalScopeAttribute : Attribute {
    public readonly int type;
    public readonly bool dynamic;

    /// <summary></summary>
    public CreatesInternalScopeAttribute(int Type, bool Dynamic = false) {
        this.type = Type;
        this.dynamic = Dynamic;
    }
}

/// <summary>
/// (BDSL2) The marked method automatically declared variables in its enclosing lexical scope.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
public class ExtendsInternalScopeAttribute : Attribute {
    public readonly int type;

    /// <summary></summary>
    public ExtendsInternalScopeAttribute(int Type) {
        this.type = Type;
    }
}

/// <summary>
/// The marked method is an expression function that modifies some of its arguments.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AssignsAttribute : Attribute {
    public int[] Indices { get; }

    /// <summary></summary>
    public AssignsAttribute(params int[] indices) {
        this.Indices = indices;
    }
}

/// <summary>
/// The marked method can only be reflected in BDSL2.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class BDSL2OnlyAttribute : Attribute { }

/// <summary>
/// The marked method is a BDSL2 operator. It should not be permitted for reflection in BDSL2.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class BDSL2OperatorAttribute : Attribute { }

/// <summary>
/// Special workaround for handling the multiply operator in BDSL2. Some overloads
///  should be skipped if any of the argument types are int.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class BDSL2MULTIPLY_OPERATORAttribute : Attribute {
    public readonly bool isIntValid;
    /// <summary></summary>
    public BDSL2MULTIPLY_OPERATORAttribute(bool isIntValid) {
        this.isIntValid = isIntValid;
    }
}

/// <summary>
/// (Informational) The marked method is, semantically speaking, an operator.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class OperatorAttribute : Attribute { }

// --- BDSL1 attributes

[AttributeUsage(AttributeTargets.Method)]
public class WarnOnStrictAttribute : Attribute {
    public readonly int strictness;
    public WarnOnStrictAttribute(int strict = 1) {
        strictness = strict;
    }
}

/// <summary>
/// For methods that cannot be specialized by their return type in BDSL1 (eg. equality has type (T,T)->bool),
///  automatically specialize the generic type at the provided index with the provided type.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class BDSL1AutoSpecializeAttribute : Attribute {
    public readonly int typeIndex;
    public readonly Type specializeAs;

    /// <summary></summary>
    public BDSL1AutoSpecializeAttribute(int typeIndex, Type specializeAs) {
        this.typeIndex = typeIndex;
        this.specializeAs = specializeAs;
    }
}