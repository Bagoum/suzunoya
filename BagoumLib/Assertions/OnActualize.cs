using System;
using System.Reactive;
using System.Threading.Tasks;

namespace BagoumLib.Assertions {
/// <summary>
/// An assertion that invokes a callback when it is actualized (but not when it is inherited).
/// </summary>
/// <param name="onActualize"></param>
public record OnActualize(Func<Task> onActualize) : IAssertion<OnActualize> {
    /// <inheritdoc />
    public string? ID { get; init; }
    /// <inheritdoc />
    public (int Phase, int Ordering) Priority { get; init; }
    /// <inheritdoc />
    public Task ActualizeOnNewState() => onActualize();
    /// <inheritdoc />
    public Task ActualizeOnNoPreceding() => onActualize();
    /// <inheritdoc />
    public Task DeactualizeOnEndState() => Task.CompletedTask;
    /// <inheritdoc />
    public Task DeactualizeOnNoSucceeding() => Task.CompletedTask;
    
    /// <inheritdoc />
    public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
    /// <inheritdoc />
    public Task _Inherit(OnActualize prev) => Task.CompletedTask;
}
}