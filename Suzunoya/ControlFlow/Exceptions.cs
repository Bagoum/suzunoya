using System;
using Suzunoya.Entities;

namespace Suzunoya.ControlFlow {
/// <summary>
/// Exception thrown when operating over an <see cref="IEntity"/> that has been destroyed.
/// </summary>
public class DestroyedObjectException : Exception {
    /// <inheritdoc cref="DestroyedObjectException"/>
    public DestroyedObjectException(string message) : base(message) { }
}
}