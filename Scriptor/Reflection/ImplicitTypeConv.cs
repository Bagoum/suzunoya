using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Scriptor.Compile;
using Scriptor.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace Scriptor.Reflection;

/// <summary>
/// The kind of scoped conversion created by an <see cref="IScopedTypeConverter"/>.
/// </summary>
public enum ScopedConversionKind {
    /// <summary>
    /// A conversion method that compiles an expression and thus creates a local scope, and which
    ///  should treat any local declarations (var) as within Ex.Block scope.
    /// </summary>
    BlockScopedExpression,
    
    /// <summary>
    /// A conversion method that compiles an expression and thus creates a local scope, and which
    ///  should use environment frames to instantiate scopes.
    /// <br/>Because this incurs high garbage/computational overhead in repeated invocations of compiled expressions,
    ///  this is only used for GCXF of StateMachine, AsyncPattern, and SyncPattern, where it is necessary.
    /// </summary>
    EFScopedExpression,
    
    /// <summary>
    /// A conversion method not interfacing with expressions, such as GenCtxProperty[] to GenCtxProperties{X}.
    /// </summary>
    Trivial
}

/// <summary>
/// An implicit type converter that may introduce a new scope, such as for an expression compiler.
/// </summary>
public interface IScopedTypeConverter : IImplicitTypeConverter {
    /// <summary>
    /// Arguments implicit to the scope. If this is null, then a new scope will not be created.
    /// </summary>
    IDelegateArg[]? ScopeArgs { get; }
    
    /// <inheritdoc cref="ScopedConversionKind"/>
    ScopedConversionKind Kind { get; }
}

/// <summary>
/// An implicit type converter whose instances can be exclusively consumed by single users.
/// </summary>
public interface ITypeConvWithInstance : IImplicitTypeConverter {
    /// <summary>
    /// The common method type, with generics, for this type converter.
    /// The <see cref="TypeDesignation.Variable"/> instances in this should not be mapped.
    /// </summary>
    TypeDesignation.Dummy SharedMethodType { get; }
    
    /// <summary>
    /// Set a new <see cref="IImplicitTypeConverter.NextInstance"/>.
    /// </summary>
    void SetNextInstance(IImplicitTypeConverterInstance next);
    
    /// <inheritdoc cref="IImplicitTypeConverterInstance"/>
    public class Instance : IImplicitTypeConverterInstance {
        private ITypeConvWithInstance Converter { get; }
        /// <summary>
        /// The method type from <see cref="Converter"/> recreated with local variables.
        /// </summary>
        public TypeDesignation.Dummy MethodType { get; }
        /// <inheritdoc/>
        public TypeDesignation.Variable[] Generic { get; }
        IImplicitTypeConverter IImplicitTypeConverterInstance.Converter => Converter;

        /// <inheritdoc cref="IImplicitTypeConverterInstance"/>
        public Instance(ITypeConvWithInstance conv) {
            Converter = conv;
            MethodType = conv.SharedMethodType.RecreateVariablesD();
            Generic = MethodType.GetVariables().Distinct().ToArray();
        }

        /// <summary>
        /// Mark that this instance has been consumed and should not be used by any other consumers.
        /// </summary>
        public void MarkUsed() {
            if (Generic.Length > 0)
                Converter.SetNextInstance(new Instance(Converter));
        }

        /// <summary>
        /// Finalize this conversion as a <see cref="RealizedImplicitCast"/>.
        /// </summary>
        public IRealizedImplicitCast Realize(Unifier u) => new RealizedImplicitCast(this, u);
    }
}


/// <summary>
/// Implicit type conversion for non-generic types.
/// </summary>
/// Note: even if the type is non-generic, usage of "implicitly generic" tex types, such as
///  Func&lt;TExArgCtx, TEx&gt;, can result in the method type having variables.
public abstract class FixedImplicitTypeConv : IScopedTypeConverter, ITypeConvWithInstance {
    /// <inheritdoc cref="ITypeConvWithInstance.SharedMethodType"/>
    public abstract TypeDesignation.Dummy MethodType { get; }
    TypeDesignation.Dummy ITypeConvWithInstance.SharedMethodType => MethodType;
    /// <summary>
    /// Implcicit arguments provided by the conversion.
    /// </summary>
    public IDelegateArg[]? ScopeArgs { get; init; }
    /// <inheritdoc cref="ScopedConversionKind"/>
    public ScopedConversionKind Kind { get; init; } = ScopedConversionKind.Trivial;
    
    /// <inheritdoc/>
    //todo: this must be constructed in inheriting type constructors so they have their instances ready
    public IImplicitTypeConverterInstance NextInstance { get; protected set; } = null!;
    
    /// <inheritdoc/>
    public void SetNextInstance(IImplicitTypeConverterInstance next) => NextInstance = next;

    /// <summary>
    /// Apply this conversion to an expression.
    /// </summary>
    public abstract TEx Convert(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac);
}
/// <summary>
/// Implicit type conversion from type T to type R.
/// </summary>
public class FixedImplicitTypeConv<T, R> : FixedImplicitTypeConv {
    /// <inheritdoc/>
    public override TypeDesignation.Dummy MethodType { get; } = 
        TypeDesignation.Dummy.Method(
            TypeDesignation.FromType(typeof(R)),
            TypeDesignation.FromType(typeof(T)));

    private record ConvMethod {
        public record DirectFunc(Expression<Func<T, R>> Conv) : ConvMethod {
            public bool AllowConstConversion { get; set; } = false;
            private Func<T, R>? _constConv = null;
            public Func<T, R> ConstConv => _constConv ??= Conv.Compile();
        }

        public record TacGeneratedFunc(Func<Func<TExArgCtx, TEx>, Func<TExArgCtx, TEx<R>>> Conv) : ConvMethod;
    }
    private readonly ConvMethod convMethod;

    /// <inheritdoc cref="FixedImplicitTypeConv"/>
    protected FixedImplicitTypeConv(Expression<Func<T, R>> converter, bool allowConst) {
        this.convMethod = new ConvMethod.DirectFunc(converter) { AllowConstConversion = allowConst };
        NextInstance = new ITypeConvWithInstance.Instance(this);
    }
    
    /// <inheritdoc cref="FixedImplicitTypeConv"/>
    public FixedImplicitTypeConv(Func<Func<TExArgCtx, TEx>, Func<TExArgCtx, TEx<R>>> converter) {
        this.convMethod = new ConvMethod.TacGeneratedFunc(converter);
        NextInstance = new ITypeConvWithInstance.Instance(this);
    }

    /// <summary>
    /// Create a <see cref="FixedImplicitTypeConv"/> from a function.
    /// </summary>
    public static FixedImplicitTypeConv<T,R> FromFn(Expression<Func<T, R>> converter, 
        ScopedConversionKind kind = ScopedConversionKind.Trivial, bool allowConst = false) =>
        new(converter, allowConst) { Kind = kind };
    
    /// <inheritdoc/>
    public override TEx Convert(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) {
        if (convMethod is ConvMethod.DirectFunc dc) {
            var content = castee(tac);
            if ((Ex)content is ConstantExpression { Value: T obj } && dc.AllowConstConversion)
                return (TEx<R>)Ex.Constant(dc.ConstConv(obj), typeof(R));
            else
                return (TEx<R>)new ReplaceParameterVisitor(dc.Conv.Parameters[0], content).Visit(dc.Conv.Body);
        } else if (convMethod is ConvMethod.TacGeneratedFunc tgf) {
            return tgf.Conv(castee)(tac);
        } else
            throw new ArgumentOutOfRangeException(convMethod.GetType().RName());
    }
}

/// <summary>
/// Implicit type conversions with one generic type.
/// </summary>
public abstract record GenericTypeConv1 : IScopedTypeConverter, ITypeConvWithInstance {
    /// <inheritdoc cref="ITypeConvWithInstance.SharedMethodType"/>
    public TypeDesignation.Dummy SharedMethodType { get; }
    /// <inheritdoc cref="FixedImplicitTypeConv.ScopeArgs"/>
    public IDelegateArg[]? ScopeArgs { get; init; }
    /// <inheritdoc cref="FixedImplicitTypeConv.Kind"/>
    public ScopedConversionKind Kind { get; init; } = ScopedConversionKind.Trivial;
    private static readonly Dictionary<Type, MethodInfo> converters = new();
    private static readonly MethodInfo mi = typeof(GenericTypeConv1).GetMethod(nameof(Convert))!;
    
    /// <inheritdoc/>
    public IImplicitTypeConverterInstance NextInstance { get; private set; }
    /// <inheritdoc/>
    /// 
    public void SetNextInstance(IImplicitTypeConverterInstance next) => NextInstance = next;
    
    /// <inheritdoc cref="GenericTypeConv1"/>
    protected GenericTypeConv1(TypeDesignation.Dummy SharedMethodType) {
        this.SharedMethodType = SharedMethodType;
        NextInstance = new ITypeConvWithInstance.Instance(this);
    }

    /// <inheritdoc cref="FixedImplicitTypeConv.Convert"/>
    public abstract TEx<T> Convert<T>(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac);

    /// <inheritdoc cref="FixedImplicitTypeConv.Convert"/>
    public virtual TEx ConvertForType(Type t, IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) {
        var conv = converters.TryGetValue(t, out var c) ? c : converters[t] = mi.MakeGenericMethod(t);
        return (TEx)conv.Invoke(this, [ast, castee, tac])!;
    }

}

/// <summary>
/// Implicit converter that converts a singleton into an array.
/// </summary>
public record SingletonToArrayConv() : GenericTypeConv1(SharedType) {
    private static TypeDesignation.Dummy MakeSharedTypeSingleton() {
        var v = new TypeDesignation.Variable();
        return TypeDesignation.Dummy.Method(v.MakeArrayType(), v);
    }
    private static readonly TypeDesignation.Dummy SharedType = MakeSharedTypeSingleton();

    /// <inheritdoc/>
    public override TEx<T> Convert<T>(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) {
        var content = castee(tac);
        if ((Ex)content is ConstantExpression { Value: T obj })
            return Ex.Constant(new[] { obj });
        else
            return Ex.NewArrayInit(typeof(T), content);
    }
}

/// <summary>
/// Implicit converter that uses a method to convert an input into an output.
/// </summary>
public class MethodConv1 : FixedImplicitTypeConv {
    /// <inheritdoc/>
    public override TypeDesignation.Dummy MethodType => Mi.SharedType;
    /// <summary>
    /// The method used for conversion.
    /// </summary>
    public MethodSignature Mi { get; }
    
    /// <inheritdoc cref="MethodConv1"/>
    public MethodConv1(MethodSignature Mi) {
        this.Mi = Mi;
        this.Kind = Mi.ImplicitTypeConvKind;
        NextInstance = new ITypeConvWithInstance.Instance(this);
    }

    /// <inheritdoc/>
    public override TEx Convert(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) =>
        AST.MethodCall.RealizeMethod(ast, Mi, tac, (_, tac) => castee(tac), true);
}

/// <summary>
/// Implicit converter that uses a generic method to convert an input into an output.
/// </summary>
public record GenericMethodConv1 : GenericTypeConv1 {
    /// <summary>
    /// The generic method used for conversion.
    /// </summary>
    public GenericMethodSignature GMi { get; }
    
    /// <inheritdoc cref="GenericMethodConv1"/>
    public GenericMethodConv1(GenericMethodSignature GMi) : base(GMi.SharedType) {
        this.GMi = GMi;
        this.Kind = GMi.ImplicitTypeConvKind;
    }
    
    /// <inheritdoc/>
    public override TEx<T> Convert<T>(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) => throw new NotImplementedException();

    /// <inheritdoc/>
    public override TEx ConvertForType(Type t, IAST ast,  Func<TExArgCtx, TEx> castee, TExArgCtx tac) =>
        AST.MethodCall.RealizeMethod(ast, GMi.Specialize(t), tac, (_, tac) => castee(tac), true);
}
