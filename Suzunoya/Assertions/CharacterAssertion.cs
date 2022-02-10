using System;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Assertions;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;

namespace Suzunoya.Assertions {
public record CharacterAssertion<C>(IVNState vn) : EntityAssertion<C>(vn), IAssertion<CharacterAssertion<C>> where C : ICharacter, new() {
    public string? Emote { get; init; }

    protected override Task DefaultDynamicEntryHandler(C c) {
        var sign = c.Location.Value.X > 0 ? 1 : -1;
        c.Location.Value += sign * Vector3.One;
        c.Tint.Value = c.Tint.Value.WithA(0);
        return c.MoveBy(sign * new Vector3(-1, 0, 0), 1f).And(c.FadeTo(1, 1)).Task;
    }

    protected override Task DefaultDynamicExitHandler(C c) {
        var sign = c.Location.Value.X > 0 ? 1 : -1;
        return c.MoveBy(sign * new Vector3(1, 0, 0), 1f).And(c.FadeTo(0, 1)).Task;
    }

    protected override void Bind(C ent) {
        base.Bind(ent);
        ent.Emote.SetIdeal(Emote);
    }


    Task IAssertion.Inherit(IAssertion prev) => AssertionHelpers.Inherit<CharacterAssertion<C>>(prev, this);

    public Task _Inherit(CharacterAssertion<C> prev) {
        Bind(prev.Bound);
        return Task.CompletedTask;
    }
}
}