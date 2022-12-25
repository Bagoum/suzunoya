using System.Collections.Generic;
using JetBrains.Annotations;

namespace BagoumLib.DataStructures {
/// <summary>
/// An object holding inert data for a NodeLinkedList. Instances are cached.
/// </summary>
internal class Node<T> {
    private static readonly Stack<Node<T>> cache = new();
    /// <summary>
    /// Previous node in the list.
    /// </summary>
    public Node<T>? Prev { get; private set; }
    /// <summary>
    /// Next node in the list.
    /// </summary>
    public Node<T>? Next { get; private set; }
    /// <summary>
    /// Value of contained data.
    /// </summary>
    public T Value { get; private set; }
    /// <summary>
    /// The number of times this object has been deleted. Use this to determine if the object has been deleted during iteration.
    /// </summary>
    public int DeletedCount { get; private set; } = 0;

    private Node(T t) {
        Value = t;
    }

    public static Node<T> Make(T t) {
        if (cache.TryPop(out var n)) {
            n.Value = t;
            return n;
        } else
            return new(t);
    }

    private void SetNext(Node<T>? n) {
        Next = n;
    }

    private void SetPrev(Node<T>? n) {
        Prev = n;
    }

    /// <summary>
    /// A singly-linked list made up of <see cref="Node{T}"/>s.
    /// </summary>
    public class LinkedList {
        public Node<T>? First { get; private set; }
        public Node<T>? Last { get; private set; }
        public int Count { get; private set; } = 0;


        public Node<T> Add(T obj) => Append(Node<T>.Make(obj));

        public Node<T> AddBefore(Node<T> curr, T obj) => InsertBefore(curr, Node<T>.Make(obj));
        public Node<T> AddAfter(Node<T> curr, T obj) => InsertAfter(curr, Node<T>.Make(obj));


        private Node<T> Append(Node<T> n) {
            if (Last != null) {
                Last.Next = n;
            }
            n.Next = null;
            n.Prev = Last;
            Last = n;
            if (First == null) {
                First = n;
            }
            ++Count;
            return n;
        }

        public Node<T> InsertBefore(Node<T> curr, Node<T> toAdd) {
            if (curr.Prev == null) {
                //curr == first
                First = toAdd;
            } else {
                curr.Prev.Next = toAdd;
            }
            toAdd.Prev = curr.Prev;
            toAdd.Next = curr;
            curr.Prev = toAdd;
            ++Count;
            return toAdd;
        }

        public Node<T> InsertAfter(Node<T> curr, Node<T> toAdd) {
            if (curr.Next == null) {
                //curr == last
                Last = toAdd;
            } else {
                curr.Next.Prev = toAdd;
            }
            toAdd.Next = curr.Next;
            toAdd.Prev = curr;
            curr.Next = toAdd;
            ++Count;
            return toAdd;
        }

        /// <summary>
        /// Remove a node. The node may still be used by the caller and later reinserted.
        /// </summary>
        public void Remove(Node<T> n) {
            if (First == n) {
                First = n.Next;
            }
            if (Last == n) {
                Last = n.Prev;
            }
            n.Next?.SetPrev(n.Prev);
            n.Prev?.SetNext(n.Next);
            ++n.DeletedCount;
            --Count;
        }

        /// <summary>
        /// Remove a node and push it to the cache. The node must not be reused after this is called.
        /// </summary>
        public void Destroy(Node<T> n) {
            Remove(n);
            cache.Push(n);
        }

        public void Reset() {
            for (var n = First; n != null; n = n.Next) {
                ++n.DeletedCount;
                cache.Push(n);
            }
            First = null;
            Last = null;
            Count = 0;
        }


        /// <summary>
        /// This method is slow
        /// </summary>
        public Node<T>? At(int ii) {
            for (Node<T>? nr = First; nr != null; nr = nr.Next, --ii) {
                if (ii == 0) return nr;
            }
            return null;
        }

        /// <summary>
        /// This method is slow
        /// </summary>
        public int IndexOf(Node<T> n) {
            int ii = 0;
            for (Node<T>? nr = First; nr != null; nr = nr.Next, ++ii) {
                if (nr == n) return ii;
            }
            return -1;
        }
    }
}
}