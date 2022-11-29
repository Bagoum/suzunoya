using System;
using System.Collections.Generic;
using System.Numerics;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Reflection;

namespace Suzunoya.Entities {
/// <summary>
/// Interface for entities that have transforms.
/// </summary>
public interface ITransform : IEntity {
    /// <summary>
    /// Base location of the entity.
    /// </summary>
    IdealOverride<Vector3> Location { get; }
    
    /// <summary>
    /// Location with disturbance effects such as screenshake or displacement applied.
    /// </summary>
    DisturbedSum<Vector3> ComputedLocation { get; }
    
    /// <summary>
    /// Base euler angles of the entity.
    /// </summary>
    IdealOverride<Vector3> EulerAnglesD { get; }
    
    /// <summary>
    /// Euler angles with disturbance effects applied.
    /// </summary>
    DisturbedSum<Vector3> ComputedEulerAnglesD { get; }
    
    /// <summary>
    /// Base scale of the entity.
    /// </summary>
    IdealOverride<Vector3> Scale { get; }
    
    /// <summary>
    /// Scale with disturbance effects applied.
    /// </summary>
    DisturbedProduct<Vector3> ComputedScale { get; }
    
    /// <summary>
    /// Children of this transform.
    /// </summary>
    IEnumerable<ITransform> Children { get; }
    /// <summary>
    /// Parent of this transform.
    /// </summary>
    ITransform? Parent { get; set; }
    /// <summary>
    /// Add a child to this object's <see cref="Children"/> list.
    /// <br/>Any modification of child properties caused by parenting should be handled by the caller,
    /// not this function.
    /// </summary>
    IDisposable NotifyChildCreated(ITransform child);
}

/// <summary>
/// An entity with a transform.
/// </summary>
public class Transform : Entity, ITransform {
    /// <inheritdoc />
    public IdealOverride<Vector3> Location { get; }
    /// <inheritdoc />
    public DisturbedSum<Vector3> ComputedLocation { get; }
    /// <inheritdoc />
    public IdealOverride<Vector3> EulerAnglesD { get; }
    /// <inheritdoc />
    public DisturbedSum<Vector3> ComputedEulerAnglesD { get; }
    /// <inheritdoc />
    public IdealOverride<Vector3> Scale { get; }
    /// <inheritdoc />
    public DisturbedProduct<Vector3> ComputedScale { get; }
    /// <summary>
    /// Tokens that control dependencies on the parent.
    /// </summary>
    protected readonly List<IDisposable> parentTokens = new();
    private readonly DMCompactingArray<ITransform> children = new(1);
    private ITransform? parent;
    
    /// <inheritdoc />
    public IEnumerable<ITransform> Children => children;
    /// <inheritdoc />
    public ITransform? Parent {
        get => parent;
        set {
            if (value == parent) return;
            parentTokens.DisposeAll();
            parent = null;
            if (value != null)
                BindParent(value);
        }
    }

    /// <summary>
    /// Create a transform entity.
    /// </summary>
    public Transform(Vector3? location = null, Vector3? eulerAnglesD = null, Vector3? scale = null) {
        Location = new(location ?? Vector3.Zero);
        ComputedLocation = new(Location);
        EulerAnglesD = new(eulerAnglesD ?? Vector3.Zero);
        ComputedEulerAnglesD = new(EulerAnglesD);
        Scale = new(scale ?? Vector3.One);
        ComputedScale = new(Scale);
    }

    /// <summary>
    /// Set the parent of this transform. This means that the location, euler angles, and scale will be
    ///  offset by the parent's values.
    /// </summary>
    protected virtual void BindParent(ITransform nParent) {
        Container.Logs.OnNext($"{this} is set as a child of {nParent}.");
        parent = nParent;
        parentTokens.Add(nParent.EntityActive.Subscribe(b => {
            if (!b) {
                Container.Logs.OnNext($"{this} has received a cascading deletion from a parent.");
                //Cascading deletions should be soft since they may occur in cases where
                // parent and child are simultaneously running exit animations (and the parent finishes first),
                // and these should not cause hard cancellations to be raised.
                SoftDelete();
            }
        }));
        parentTokens.Add(ComputedLocation.AddDisturbance(nParent.ComputedLocation));
        parentTokens.Add(ComputedEulerAnglesD.AddDisturbance(nParent.ComputedEulerAnglesD));
        parentTokens.Add(ComputedScale.AddDisturbance(nParent.ComputedScale));
        parentTokens.Add(nParent.NotifyChildCreated(this));
    }
    /// <inheritdoc />
    public IDisposable NotifyChildCreated(ITransform child) => children.Add(child);
}

}