using System;
using System.Reactive;
using System.Threading.Tasks;

namespace BagoumLib.Assertions {
/// <summary>
/// An assertion that sets the given <see cref="TaskCompletionSource{Unit}"/> when it is actualized.
/// </summary>
public record CompletionAssertion(TaskCompletionSource<Unit> c) : IAssertion<CompletionAssertion> {
    /// <inheritdoc />
    public Task ActualizeOnNewState() {
        c.SetResult(default);
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public Task ActualizeOnNoPreceding() => ActualizeOnNewState();
    /// <inheritdoc />
    public Task DeactualizeOnEndState() => throw new NotImplementedException();
    /// <inheritdoc />
    public Task DeactualizeOnNoSucceeding() => throw new NotImplementedException();
    
    /// <inheritdoc />
    public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
    /// <inheritdoc />
    public Task _Inherit(CompletionAssertion prev) => throw new NotImplementedException();
}
}