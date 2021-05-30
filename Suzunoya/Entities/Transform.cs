using System.Numerics;
using BagoumLib.Events;

namespace Suzunoya.Entities {
public interface ITransform : IEntity {
    Evented<Vector3> Location { get; }
    Evented<Vector3> EulerAnglesD { get; }
    Evented<Vector3> Scale { get; }
}

public class Transform : Entity, ITransform {
    public Evented<Vector3> Location { get; }
    public Evented<Vector3> EulerAnglesD { get; }
    public Evented<Vector3> Scale { get; }
    
    public Transform(Vector3? location = null, Vector3? eulerAnglesD = null, Vector3? scale = null) {
        Location = new(location ?? Vector3.Zero);
        EulerAnglesD = new(eulerAnglesD ?? Vector3.Zero);
        Scale = new(scale ?? Vector3.One);
    }
}

}