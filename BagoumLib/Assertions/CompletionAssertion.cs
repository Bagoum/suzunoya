using System;
using System.Reactive;
using System.Threading.Tasks;

namespace BagoumLib.Assertions {
public record CompletionAssertion(TaskCompletionSource<Unit> c) : IAssertion<CompletionAssertion> {
    public Task ActualizeOnNewState() {
        c.SetResult(default);
        return Task.CompletedTask;
    }
    public Task ActualizeOnNoPreceding() => ActualizeOnNewState();
    public Task DeactualizeOnEndState() => throw new NotImplementedException();
    public Task DeactualizeOnNoSucceeding() => throw new NotImplementedException();
    
    public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
    public Task _Inherit(CompletionAssertion prev) => throw new NotImplementedException();
}
}