using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;

namespace Suzunoya.Assertions {
/// <summary>
/// Base class for assertions that have tokens bound to their lifetime.
/// </summary>
public record TokenizedAssertion {
    /// <summary>
    /// Set of tokens bound to the assertion lifetime.
    /// </summary>
    protected List<IDisposable> Tokens { get; } = new();

    /// <inheritdoc cref="IAssertion.DeactualizeOnEndState"/>
    public Task DeactualizeOnEndState() {
        Tokens.DisposeAll();
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="IAssertion.DeactualizeOnNoSucceeding"/>
    public Task DeactualizeOnNoSucceeding() => DeactualizeOnEndState();
    
    /// <inheritdoc cref="IAssertion{T}._Inherit"/>
    public Task _Inherit(TokenizedAssertion prev) {
        prev.Tokens.DisposeAll();
        return Task.CompletedTask;
    }
}
}