using System;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Assertions;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;

namespace Suzunoya.Assertions {
/// <summary>
/// Assertions that run over Suzunoya <see cref="ICharacter"/> objects.
/// </summary>
public record CharacterAssertion<C>(IVNState vn) : EntityAssertion<C>(vn), IAssertion<CharacterAssertion<C>> where C : ICharacter, new() {
    /// <summary>
    /// Bound to <see cref="ICharacter.Emote"/>
    /// </summary>
    public string? Emote { get; init; }

    /// <inheritdoc/>
    protected override Task DefaultDynamicEntryHandler(C c) {
        var sign = c.LocalLocation.Value.X > 0 ? 1 : -1;
        c.LocalLocation.Value += sign * Vector3.UnitX;
        c.Tint.Value = c.Tint.Value.WithA(0);
        return c.MoveBy(sign * new Vector3(-1, 0, 0), 1f).And(c.FadeTo(1, 1)).Task;
    }

    /// <inheritdoc/>
    protected override Task DefaultDynamicExitHandler(C c) {
        var sign = c.LocalLocation.Value.X > 0 ? 1 : -1;
        return c.MoveBy(sign * new Vector3(1, 0, 0), 1f).And(c.FadeTo(0, 1)).Task;
    }

    /// <inheritdoc/>
    protected override void Bind(C ent) {
        base.Bind(ent);
        ent.Emote.SetIdeal(Emote);
    }
    
    Task IAssertion.Inherit(IAssertion prev) => AssertionHelpers.Inherit<CharacterAssertion<C>>(prev, this);

    /// <inheritdoc/>
    public Task _Inherit(CharacterAssertion<C> prev) {
        Bind(prev.Bound);
        return Task.CompletedTask;
    }
}
}