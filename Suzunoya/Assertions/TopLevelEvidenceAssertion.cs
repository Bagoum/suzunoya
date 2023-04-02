using System;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using Suzunoya.ADV;
using Suzunoya.ControlFlow;

namespace Suzunoya.Assertions {
/// <summary>
/// An assertion that provides a top-level evidence handler (ie. one that can receive evidence while no dialogue is running) for <see cref="ADVEvidenceRequest{E}"/>.
/// </summary>
public record TopLevelEvidenceAssertion<E, T>(ADVEvidenceRequest<E> Requester, Func<E, BoundedContext<T>>? Handler) : TokenizedAssertion, IAssertion<TopLevelEvidenceAssertion<E, T>> {

    /// <inheritdoc />
    public Task ActualizeOnNewState() {
        Tokens.Add(Requester.RequestTopLevel(Handler));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ActualizeOnNoPreceding() => ActualizeOnNewState();

    Task IAssertion.Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
    
    /// <inheritdoc />
    public Task _Inherit(TopLevelEvidenceAssertion<E, T> prev) {
        prev.Tokens.DisposeAll();
        return ActualizeOnNewState();
    }
}
}