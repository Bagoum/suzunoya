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
//Any processes not directly managed by the VNState should follow an object-delegation pattern. 
// They have some sort of "Skip" and "Start" methods, and the VNState will decide which of these
// to call based on internal parameters.
public interface IInterrogator {
    /// <summary>
    /// Key associated with the output value of this question in the save data.
    /// If the key is null, the output value will not be saved, but only for null scripts.
    /// </summary>
    public string? Key { get; }
 }
public interface IInterrogator<T> : IInterrogator {
    /// <summary>
    /// Called by VNState when a value for the question already exists in the save data.
    /// </summary>
    public void Skip(T existing);

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
    public string? Key { get; }
    public Evented<Action<T>?> AwaitingResponse { get; } = new(null);

    public Evented<bool> EntityActive { get; } = new(true);

    public Interrogator(string? key = null) {
        Key = key;
    }

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
    
    public ChoiceInterrogator(string? key, params (T, string)[] choices) : base(key) {
        Choices = choices.ToArray();
    }
    public ChoiceInterrogator(params (T, string)[] choices) : this(null, choices) { }
}

//imagine not having higher kinded types. this meme was brought to you by haskell gang
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