using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using JetBrains.Annotations;
using Scriptor.Analysis;

// ReSharper disable CollectionNeverQueried.Global

namespace Scriptor.Expressions;

/// <summary>
/// Helper class for tracking AOT compilation data.
/// </summary>
[PublicAPI]
public abstract class ExBakeTracker {
    private uint proxyArgNum = 0;
    /// <summary>
    /// The next identifier for a proxy argument.
    /// </summary>
    /// <returns></returns>
    public string NextProxyArg() => $"proxy{proxyArgNum++}";
    /// <summary>
    /// Store a non-printable argument so it can be used to generate
    ///  a script as a function or provide arguments to such a function.
    /// </summary>
    public virtual object Proxy(object replacee, Type t) => replacee;

    /// <summary>
    /// No support for AOT compilation.
    /// </summary>
    public class None : ExBakeTracker;

    /// <summary>
    /// AOT compilation in save mode, tracking replacements and declarations
    ///  in order to allow printing expressions into code.
    /// </summary>
    public class Save : ExBakeTracker {
        private readonly Expression args = Expression.Variable(typeof(object[]), "args");
        /// <summary>
        /// Variable declarations hoisted above the lambdas of compiled expressions.
        /// </summary>
        public List<string> HoistedVariables { get; } = new();
        /// <summary>
        /// Replacements matching an expression in the source tree to a simplified expression
        ///  (eg. to a hoisted variable declaration or function argument).
        /// </summary>
        public Dictionary<Expression, Expression> HoistedReplacements { get; } = new();
        /// <summary>
        /// Replacements matching a constant value that may occur in ConstantExpression to a simplified expression
        ///  (eg. to a hoisted variable declaration or function argument).
        /// </summary>
        public Dictionary<object, Expression> HoistedConstants { get; } = new();
        /// <summary>
        /// Replacements matching an expression for calling a function recursively to its script declaration.
        /// </summary>
        public Dictionary<Expression, ScriptFnDecl> RecursiveFunctions { get; } = new();
        
        /// <summary>
        /// The types and names of the arguments used if this represents a function.
        /// </summary>
        public List<(Type, string)> ProxyTypes { get; } = new();
        
        /// <inheritdoc/>
        public override object Proxy(object replacee, Type t) {
            var name = NextProxyArg();
            HoistedConstants[replacee] = args.Index(Expression.Constant(ProxyTypes.Count)).Cast(t);  
            ProxyTypes.Add((t, name));
            return base.Proxy(replacee, t);
        }
    }

    /// <summary>
    /// AOT compilation in load mode, tracking proxy arguments to provide
    ///  to pre-compiled functions from <see cref="ExBakeTracker.Save"/>.
    /// </summary>
    public class Load : ExBakeTracker {
        /// <summary>
        /// Arguments to be provided to a pre-compiled function.
        /// </summary>
        public List<object> ProxyArguments { get; } = [];
        
        /// <inheritdoc/>
        public override object Proxy(object replacee, Type t) {
            ProxyArguments.Add(replacee);
            return base.Proxy(replacee, t);
        }
    }
}
