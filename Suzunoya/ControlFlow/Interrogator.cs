using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Tasks;
using JetBrains.Annotations;
using Suzunoya.Dialogue;

namespace Suzunoya.ControlFlow {
/// <summary>
/// An interface representing a question asked of the player.
/// </summary>
public interface IInterrogator { }
public interface IInterrogator<T> : IInterrogator {
    /// <summary>
    /// Called by VNState to start the interrogation process.
    /// </summary>
    public Task<T> Start(ICancellee cT);
    
    /// <summary>
    /// A function that can be called to provide a response to the interrogator.
    /// Note: consumers should switch over more specific interfaces/classes (eg. ChoiceInterrogator)
    ///  to determine the question, format, possible responses, etc.
    /// </summary>
    public Evented<Action<T>?> AwaitingResponse { get; }
    Evented<bool> EntityActive { get; }
}

public class Interrogator<T> : IInterrogator<T> {
    public Evented<Action<T>?> AwaitingResponse { get; } = new(null);

    public Evented<bool> EntityActive { get; } = new(true);


    public void Skip(T existing) {
        EntityActive.OnNext(false);
    }

    public async Task<T> Start(ICancellee cT) {
        AwaitingResponse.Value = WaitingUtils.GetAwaiter(out Task<T> t);
        var v = await t;
        AwaitingResponse.Value = null;
        EntityActive.OnNext(false);
        return v;
    }
}

public class ChoiceInterrogator<T> : Interrogator<T> {
    public IReadOnlyList<(T value, string description)> Choices { get; }
    
    public ChoiceInterrogator(params (T, string)[] choices) {
        Choices = choices.ToArray();
    }
}


/* Would be really nice to have type constructors so I could generically write
public interface IGenericEvent<T<>> {
    public void OnNext<B>(T<B> data);
}
 */
public interface IInterrogatorReceiver {
    public void OnNext<T>(IInterrogator<T> data);
}

public interface IInterrogatorConstructor {
    public IDisposable Subscribe(IInterrogatorReceiver obs);
}

public interface IInterrogatorSubject : IInterrogatorReceiver, IInterrogatorConstructor {
}

public class InterrogatorEvent : IInterrogatorSubject {
    private readonly DMCompactingArray<IInterrogatorReceiver> callbacks = new();

    public void OnNext<T>(IInterrogator<T> data) {
        var ct = callbacks.Count;
        for (int ii = 0; ii < ct; ++ii) {
            if (callbacks.ExistsAt(ii))
                callbacks[ii].OnNext(data);
        }
        callbacks.Compact();
    }

    public IDisposable Subscribe(IInterrogatorReceiver obs) => callbacks.Add(obs);
}

}