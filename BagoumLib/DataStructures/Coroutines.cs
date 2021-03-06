using System;
using System.Collections;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// Details on how to handle a newly-added coroutine.
/// </summary>
public enum CoroutineType {
    /// <summary>
    /// If a currently executing ienumerator exists, adds the coroutine before it and steps it in place (StepPrepend).
    /// Otherwise append to end and step it in place.
    /// <br/>For inter-object dependencies, this is more correct than TryStepPrepend if sending an iEnum to a previous object in the update order.
    /// </summary>
    StepTryPrepend,
    /// <summary>
    /// If a currently executing ienumerator exists, adds the coroutine before it and steps it in place (StepPrepend).
    /// Otherwise append to end (AppendToEnd).
    /// <br/>For inter-object dependencies, this is more correct than StepTryPrepend if sending an iEnum to a later object in the update order.
    /// This is used by default. This means that sending an update to a previous object in the update order will cause it to execute one frame slow.
    /// </summary>
    TryStepPrepend,
    /// <summary>
    /// Adds the coroutine before the currently executing ienumerator and steps it in place.
    /// If no currently executing ienumerator exists, throws an error.
    /// </summary>
    StepPrepend,
    /// <summary>
    /// Adds the coroutine to the end of the executing list.
    /// </summary>
    AppendToEnd
}

/// <summary>
/// Options detailing how to handle a new coroutine.
/// </summary>
/// <param name="Droppable">True iff the coroutine can be deleted without waiting for cancellation when
///  the coroutine container is deleted.</param>
/// <param name="ExecType">Instructions on how to add the new coroutine to the existing list.</param>
public record CoroutineOptions(bool Droppable = false, CoroutineType ExecType = CoroutineType.TryStepPrepend) {
    public static readonly CoroutineOptions Default = new();
    public static readonly CoroutineOptions DroppableDefault = new(true);
}

/// <summary>
/// An object on which coroutines can be run.
/// </summary>
[PublicAPI]
public interface ICoroutineRunner {
    void Run(IEnumerator ienum, CoroutineOptions? opts = null);
}

/// <summary>
/// A class that permits manually stepping through IEnumerator-based coroutines.
/// </summary>
[PublicAPI]
public class Coroutines : ICoroutineRunner {
    private readonly struct RCoroutine {
        public readonly IEnumerator ienum;
        public readonly Node<RCoroutine>? parent;
        public readonly bool droppable;

        public RCoroutine(IEnumerator ienum, Node<RCoroutine>? parent = null, bool droppable = false) {
            this.ienum = ienum;
            this.parent = parent;
            this.droppable = droppable;
        }
    }

    private readonly Node<RCoroutine>.LinkedList coroutines = new();

    public IEnumerator AsIEnum() {
        while (coroutines.Count > 0) {
            Step();
            if (coroutines.Count == 0) yield break;
            yield return null;
        }
    }

    private Node<RCoroutine>? itrNode = null;

    /// <summary>
    /// Step once through all coroutines.
    /// </summary>
    public void Step() {
        Node<RCoroutine>? nextNode;
        for (var n = itrNode = coroutines.First; n != null; n = itrNode = nextNode) {
            bool remaining = n.obj.ienum.MoveNext();
            if (n.Deleted) {
                if (coroutines.Count != 0)
                    throw new Exception($"Since a coroutine node was externally deleted, expected all nodes to be " +
                                        $"deleted, but {coroutines.Count} yet exist.");
                break;
            } else if (remaining) {
                if (n.obj.ienum.Current is IEnumerator ienum) {
                    nextNode = coroutines.AddAfter(n, new RCoroutine(ienum, n, n.obj.droppable));
                    coroutines.Remove(n);
                } else
                    nextNode = n.Next;
            } else {
                if (n.obj.parent != null)
                    coroutines.InsertAfter(n, n.obj.parent);
                nextNode = n.Next;
                coroutines.Remove(n);
            }
        }
        //Important for break case.
        itrNode = null;
    }
    

    private void StepInPlace(Node<RCoroutine> n) {
        //Roughly copied from step function
        var lastItrNode = itrNode;
        itrNode = n;
        bool remaining = n.obj.ienum.MoveNext();
        if (n.Deleted) {
            //pass
        } else if (remaining) {
            if (n.obj.ienum.Current is IEnumerator ienum) {
                var nxt = coroutines.AddAfter(n, new RCoroutine(ienum, n, n.obj.droppable));
                coroutines.Remove(n);
                StepInPlace(nxt);
            }
        } else {
            if (n.obj.parent != null)
                coroutines.InsertAfter(n, n.obj.parent);
            coroutines.Remove(n);
        }
        itrNode = lastItrNode;
    }

    /// <summary>
    /// Number of coroutines currently executing.
    /// </summary>
    public int Count => coroutines.Count;

    /// <summary>
    /// Step through all coroutines once, and stops executing any droppable coroutines.
    /// <br/>Before calling this, non-droppable coroutines should be cancelled via cancellation tokens.
    /// <br/>After calling this, the caller may want to enforce that no (non-droppable) coroutines remain.
    /// </summary>
    public void Close() {
        if (coroutines.Count > 0) {
            Step();
            Node<RCoroutine>? nextNode;
            for (Node<RCoroutine>? n = coroutines.First; n != null; n = nextNode) {
                if (n.obj.droppable) {
                    if (n.obj.parent != null) {
                        coroutines.InsertAfter(n, n.obj.parent);
                    }
                    nextNode = n.Next;
                    coroutines.Remove(n);
                } else
                    nextNode = n.Next;
            }
        }
    }


    /// <summary>
    /// Run a couroutine that will be updated once every engine frame.
    /// This coroutine is expected to clean up within one step on cancellation, 
    ///  for correct behavior with <see cref="Close"/>.
    /// Alternatively, the coroutine may be marked as droppable under <see cref="CoroutineOptions.Droppable"/>.
    /// </summary>
    /// <param name="ienum">Coroutine</param>
    /// <param name="opts">Settings</param>
    public void Run(IEnumerator ienum, CoroutineOptions? opts = null) {
        opts ??= CoroutineOptions.Default;
        switch (opts.ExecType, itrNode) {
            case (CoroutineType.StepPrepend, null):
                throw new Exception("Cannot prepend when not iterating coroutines");
            case (CoroutineType.StepPrepend or CoroutineType.StepTryPrepend or CoroutineType.TryStepPrepend, not null):
                StepInPlace(coroutines.AddBefore(itrNode!, new RCoroutine(ienum, null, itrNode!.obj.droppable || opts.Droppable)));
                break;
            case (CoroutineType.StepTryPrepend, null):
                StepInPlace(coroutines.Add(new RCoroutine(ienum, null, opts.Droppable)));
                break;
            default:
                coroutines.Add(new RCoroutine(ienum, null, opts.Droppable));
                break;
        }
    }
}
}