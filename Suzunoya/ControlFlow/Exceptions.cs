using System;

namespace Suzunoya.ControlFlow {
public class DestroyedObjectException : Exception {
    public DestroyedObjectException(string message) : base(message) { }
}
}