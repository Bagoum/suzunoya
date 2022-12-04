using System;
using System.Threading.Tasks;
using BagoumLib.Assertions;

namespace Suzunoya.Assertions {
/// <summary>
/// An assertion that runs a task on entry (new state/no preceding only). Throws on inherit.
/// </summary>
public record RunOnEntryAssertion(Func<Task> OnEntry) : IAssertion<RunOnEntryAssertion> {
    /// <inheritdoc/>
    public (int Phase, int Ordering) Priority { get; set; } = (0, 0);
    /// <inheritdoc/>
    public Task ActualizeOnNewState() => OnEntry();

    /// <inheritdoc/>
    public Task ActualizeOnNoPreceding() => ActualizeOnNewState();

    /// <inheritdoc/>
    public Task DeactualizeOnEndState() => Task.CompletedTask;
    /// <inheritdoc/>
    public Task DeactualizeOnNoSucceeding() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
    /// <inheritdoc/>
    public Task _Inherit(RunOnEntryAssertion prev) => Task.CompletedTask;
}
}