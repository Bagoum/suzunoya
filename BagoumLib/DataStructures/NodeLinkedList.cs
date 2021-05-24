using JetBrains.Annotations;

namespace BagoumLib.DataStructures {

/// <summary>
/// Node holds inert data for a NodeLinkedList.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class Node<T> {
    public Node<T>? Prev { get; private set; }
    public Node<T>? Next { get; private set; }
    public readonly T obj;
    public bool Deleted { get; private set; } = false;

    public Node(T t) {
        obj = t;
    }

    private void SetNext(Node<T>? n) {
        Next = n;
    }

    private void SetPrev(Node<T>? n) {
        Prev = n;
    }

    public class LinkedList {
        public Node<T>? First { get; private set; }
        public Node<T>? Last { get; private set; }
        public int Count { get; private set; } = 0;


        public Node<T> Add(T obj) => Append(new Node<T>(obj));

        public Node<T> AddBefore(Node<T> curr, T obj) => InsertBefore(curr, new Node<T>(obj));
        public Node<T> AddAfter(Node<T> curr, T obj) => InsertAfter(curr, new Node<T>(obj));


        private Node<T> Append(Node<T> n) {
            n.Deleted = false;
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
            toAdd.Deleted = false;
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
            toAdd.Deleted = false;
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
        /// Remove a node.
        /// </summary>
        public void Remove(Node<T> n) {
            if (First == n) {
                First = n.Next;
            }
            if (Last == n) {
                Last = n.Prev;
            }
            n.Deleted = true;
            n.Next?.SetPrev(n.Prev);
            n.Prev?.SetNext(n.Next);
            --Count;
        }

        public void Reset() {
            for (var n = First; n != null; n = n.Next) {
                n.Deleted = true;
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