using System;
using System.Collections;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {

[PublicAPI]
public interface ICoroutineRunner {
    void Run(IEnumerator ienum);
    void RunDroppable(IEnumerator ienum);
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

    private readonly Node<RCoroutine>.LinkedList coroutines = new Node<RCoroutine>.LinkedList();

    public IEnumerator AsIEnum() {
        while (coroutines.Count > 0) {
            Step();
            if (coroutines.Count == 0) yield break;
            yield return null;
        }
    }

    private Node<RCoroutine>? itrNode = null;

    public void Step() {
        Node<RCoroutine>? nextNode;
        for (var n = coroutines.First; n != null; itrNode = n = nextNode) {
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

    public int Count => coroutines.Count;

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
    /// This coroutine is expected to clean up immediately on cancellation,
    /// and will throw an error if the executing object is destroyed before it is cancelled.
    /// </summary>
    /// <param name="ienum">Coroutine</param>
    public void Run(IEnumerator ienum) {
        coroutines.Add(new RCoroutine(ienum));
    }

    public void RunTryPrepend(IEnumerator ienum) {
        if (itrNode == null) Run(ienum);
        else StepInPlace(coroutines.AddBefore(itrNode, new RCoroutine(ienum, null, itrNode.obj.droppable)));
    }

    /// <summary>
    /// Run a couroutine that will be updated once every engine frame.
    /// This coroutine is expected to clean up immediately on cancellation,
    /// and will throw an error if the executing object is destroyed before it is cancelled.
    /// This function can only be called while the coroutine object is updating, and will place the new coroutine
    /// before the current iteration pointer.
    /// </summary>
    /// <param name="ienum">Coroutine</param>
    public void RunPrepend(IEnumerator ienum) {
        if (itrNode == null) throw new Exception("Cannot prepend when not iterating coroutines");
        StepInPlace(coroutines.AddBefore(itrNode, new RCoroutine(ienum, null, itrNode.obj.droppable)));
    }

    /// <summary>
    /// Run a coroutine that will be updated once every engine frame.
    /// This coroutine may be freely dropped if the object is destroyed.
    /// Use if the coroutine is not awaited by any code or has no cancellation handling.
    /// </summary>
    /// <param name="ienum">Coroutine</param>
    public void RunDroppable(IEnumerator ienum) {
        coroutines.Add(new RCoroutine(ienum, null, true));
    }
}
}