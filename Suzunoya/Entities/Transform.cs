using System;
using System.Collections.Generic;
using System.Numerics;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Reflection;

namespace Suzunoya.Entities {
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
    
    
    IEnumerable<ITransform> Children { get; }
    ITransform? Parent { get; set; }
    IDisposable NotifyChildCreated(ITransform child);
}

public class Transform : Entity, ITransform {
    public IdealOverride<Vector3> Location { get; }
    public DisturbedSum<Vector3> ComputedLocation { get; }
    public IdealOverride<Vector3> EulerAnglesD { get; }
    public DisturbedSum<Vector3> ComputedEulerAnglesD { get; }
    public IdealOverride<Vector3> Scale { get; }
    public DisturbedProduct<Vector3> ComputedScale { get; }

    protected readonly List<IDisposable> parentTokens = new();
    private readonly DMCompactingArray<ITransform> children = new(1);
    private ITransform? parent;
    
    public IEnumerable<ITransform> Children => children;
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

    public Transform(Vector3? location = null, Vector3? eulerAnglesD = null, Vector3? scale = null) {
        Location = new(location ?? Vector3.Zero);
        ComputedLocation = new(Location);
        EulerAnglesD = new(eulerAnglesD ?? Vector3.Zero);
        ComputedEulerAnglesD = new(EulerAnglesD);
        Scale = new(scale ?? Vector3.One);
        ComputedScale = new(Scale);
    }

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
    public IDisposable NotifyChildCreated(ITransform child) => children.Add(child);
}

}