using System.Collections.Generic;
using static BagoumLib.Unification.TypeDesignation;

namespace BagoumLib.Unification {
/// <summary>
/// A basic implementation of <see cref="ITypeTree"/>.
/// </summary>
public abstract record TypeTree {
    /// <summary>
    /// Degenerate interface inheriting <see cref="ITypeTree"/>.
    /// </summary>
    public interface ITree : ITypeTree { }

    public interface IMethodTree : ITree {
        ITree[] Arguments { get; }
    }
    
    /// <summary>
    /// Tree representing a method call.
    /// </summary>
    public record Method(Dummy[] Overloads, params ITree[] Arguments) : TypeTree, IMethodTree, IMethodTypeTree<Dummy> {
        IReadOnlyList<Dummy> IMethodTypeTree<Dummy>.Overloads => Overloads;
        /// <inheritdoc/>
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        /// <inheritdoc/>
        public IRealizedImplicitCast? ImplicitCast { get; set; }
        /// <inheritdoc/>
        public List<Dummy>? RealizableOverloads { get; set; }
        IReadOnlyList<ITypeTree> IMethodTypeTree<Dummy>.Arguments => Arguments;

        /// <inheritdoc/>
        public bool PreferFirstOverload { get; set; } = false;
    }

    /// <summary>
    /// Tree representing an atomic value.
    /// </summary>
    public record AtomicWithType(params TypeDesignation[] PossibleTypes) : TypeTree, ITree, IAtomicTypeTree {
        /// <inheritdoc/>
        public TypeDesignation? SelectedOverload { get; set; }

        /// <inheritdoc/>
        public IRealizedImplicitCast? ImplicitCast { get; set; }
    }
}

}