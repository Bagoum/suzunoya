using System;
using System.Reactive;
using System.Threading.Tasks;

namespace BagoumLib.Assertions {
public record OnActualize(Func<Task> onActualize) : IAssertion<OnActualize> {
    public string? ID { get; init; }
    public (int Phase, int Ordering) Priority { get; init; }
    public Task ActualizeOnNewState() => onActualize();
    public Task ActualizeOnNoPreceding() => onActualize();
    public Task DeactualizeOnEndState() => Task.CompletedTask;
    public Task DeactualizeOnNoSucceeding() => Task.CompletedTask;
    
    public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
    public Task _Inherit(OnActualize prev) => Task.CompletedTask;
}
}