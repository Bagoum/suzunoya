using System.Numerics;
using BagoumLib.Events;

namespace Suzunoya.Entities {
public interface ITransform : IEntity {
    DisturbedSum<Vector3> Location { get; }
    DisturbedSum<Vector3> EulerAnglesD { get; }
    DisturbedProduct<Vector3> Scale { get; }
}

public class Transform : Entity, ITransform {
    public DisturbedSum<Vector3> Location { get; }
    public DisturbedSum<Vector3> EulerAnglesD { get; }
    public DisturbedProduct<Vector3> Scale { get; }
    
    public Transform(Vector3? location = null, Vector3? eulerAnglesD = null, Vector3? scale = null) {
        Location = new(location ?? Vector3.Zero);
        EulerAnglesD = new(eulerAnglesD ?? Vector3.Zero);
        Scale = new(scale ?? Vector3.One);
    }
}

}