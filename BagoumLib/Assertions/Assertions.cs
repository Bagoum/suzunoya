using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Tasks;
using JetBrains.Annotations;

namespace BagoumLib.Assertions {

/// <summary>
/// A statement about the ideal status of an object in a <see cref="IdealizedState"/>,
///  providing functions that create, destroy, or modify the object as necessary.
/// </summary>
[PublicAPI]
public interface IAssertion {
    /// <summary>
    /// String that uniquely describes this assertion.
    /// <br/>This is used to pair old assertions to new assertions when the idealized state changes.
    /// <br/>Not required if all assertions have unique types.
    /// </summary>
    string? ID => null;
    
    /// <summary>
    /// Ordering priority for assertions. Lower priority assertions are executed first.
    /// <br/>Assertions are executed by phase, so all assertions in phase 0 are executed, then in phase 1, etc.
    /// </summary>
    (int Phase, int Ordering) Priority => (0, 0);

    /// <summary>
    /// Called when the state is first being actualized.
    /// </summary>
    Task ActualizeOnNewState();

    /// <summary>
    /// Called when this assertion has been added with no preceding assertion.
    /// </summary>
    Task ActualizeOnNoPreceding();
    
    /// <summary>
    /// Called when the state is being deactualized.
    /// </summary>
    Task DeactualizeOnEndState();

    /// <summary>
    /// Called when this assertion has been deleted with no succeeding assertion.
    /// </summary>
    Task DeactualizeOnNoSucceeding();
    
    /// <summary>
    /// Called when a previous assertion is replaced with this one.
    /// <br/>You should generally implement this as AssertionHelpers.Inherit(prev, this).
    /// </summary>
    /// <param name="prev">Previous assertion</param>
    Task Inherit(IAssertion prev);
}

/// <summary>
/// An <see cref="IAssertion"/> restricted to the type of the designated object.
/// </summary>
[PublicAPI]
public interface IAssertion<T> : IAssertion where T: IAssertion<T> {
    /// <summary>
    /// Called when a previous assertion is replaced with this one.
    /// </summary>
    /// <param name="prev">Previous assertion</param>
    Task _Inherit(T prev);
    
    //Note: while it is possible to define a default implementation of IAssertion.Inherit here,
    // for some reason that causes untraceable stack overflow bugs in Unity that may or may not be my fault,
    // so I've opted for just having an explicit implementation in each class.
    //Just use:
    //   public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
}

/// <summary>
/// An assertion with children, which may be defined to have some special relation to the parent.
/// For example, an EntityAssertion's children's transforms are children of that EntityAssertion's transform.
/// </summary>
[PublicAPI]
public interface IChildLinkedAssertion : IAssertion {
    /// <summary>
    /// Child assertions.
    /// </summary>
    public List<IAssertion> Children { get; }
}

/// <summary>
/// Static class providing helpers for assertions
/// </summary>
public static class AssertionHelpers {
    /// <summary>
    /// Convert the untyped assertion `prev` to type IAssertion{T}, then inherit it.
    /// </summary>
    public static Task Inherit<T>(IAssertion prev, IAssertion<T> thisNext) where T: IAssertion<T> {
        return thisNext._Inherit(prev is T obj ?
            obj :
            throw new Exception(
                $"Couldn't inherit assertion of type {prev.GetType()} for new assertion of type {typeof(T)}"));
    }
}
}