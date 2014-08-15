using System;
using System.Threading;
using System.Collections.Generic;

namespace System.Collections.Concurrent
{
    internal interface IProducerConsumerCollection<T> : IEnumerable<T>, ICollection, IEnumerable
    {
        bool TryAdd(T item);
        bool TryTake(out T item);
        T[] ToArray();
        void CopyTo(T[] array, int index);
    }

    [System.Diagnostics.DebuggerDisplay ("Count={Count}")]
    internal class ConcurrentQueue<T> : IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        class Node
        {
            public T Value;
            public Node Next;
        }

        Node head = new Node ();
        Node tail;
        int count;

        class NodeObjectPool : ObjectPool<Node> {
            protected override Node Creator ()
            {
                return new Node ();
            }
        }
        static readonly NodeObjectPool pool = new NodeObjectPool ();

        static Node ZeroOut (Node node)
        {
            node.Value = default(T);
            node.Next = null;
            return node;
        }

        public ConcurrentQueue ()
        {
            tail = head;
        }

        public ConcurrentQueue (IEnumerable<T> collection): this()
        {
            foreach (T item in collection)
                Enqueue (item);
        }

        public void Enqueue (T item)
        {
            Node node = pool.Take ();
            node.Value = item;

            Node oldTail = null;
            Node oldNext = null;

            bool update = false;
            while (!update) {
                oldTail = tail;
                oldNext = oldTail.Next;

                // Did tail was already updated ?
                if (tail == oldTail) {
                    if (oldNext == null) {
                        // The place is for us
                        update = Interlocked.CompareExchange (ref tail.Next, node, null) == null;
                    } else {
                        // another Thread already used the place so give him a hand by putting tail where it should be
                        Interlocked.CompareExchange (ref tail, oldNext, oldTail);
                    }
                }
            }
            // At this point we added correctly our node, now we have to update tail. If it fails then it will be done by another thread
            Interlocked.CompareExchange (ref tail, node, oldTail);

            Interlocked.Increment (ref count);
        }

        bool IProducerConsumerCollection<T>.TryAdd (T item)
        {
            Enqueue (item);
            return true;
        }

        public bool TryDequeue (out T result)
        {
            result = default (T);
            bool advanced = false;

            while (!advanced) {
                Node oldHead = head;
                Node oldTail = tail;
                Node oldNext = oldHead.Next;

                if (oldHead == head) {
                    // Empty case ?
                    if (oldHead == oldTail) {
                        // This should be false then
                        if (oldNext != null) {
                            // If not then the linked list is mal formed, update tail
                            Interlocked.CompareExchange (ref tail, oldNext, oldTail);
                        }
                        result = default (T);
                        return false;
                    } else {
                        result = oldNext.Value;
                        advanced = Interlocked.CompareExchange (ref head, oldNext, oldHead) == oldHead;
                        if (advanced)
                            pool.Release (ZeroOut (oldHead));
                    }
                }
            }

            Interlocked.Decrement (ref count);

            return true;
        }

        public bool TryPeek (out T result)
        {
            if (IsEmpty) {
                result = default (T);
                return false;
            }

            Node first = head.Next;
            result = first.Value;
            return true;
        }

        internal void Clear ()
        {
            count = 0;
            tail = head = new Node ();
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return (IEnumerator)InternalGetEnumerator ();
        }

        public IEnumerator<T> GetEnumerator ()
        {
            return InternalGetEnumerator ();
        }

        IEnumerator<T> InternalGetEnumerator ()
        {
            Node my_head = head;
            while ((my_head = my_head.Next) != null) {
                yield return my_head.Value;
            }
        }

        void ICollection.CopyTo (Array array, int index)
        {
            T[] dest = array as T[];
            if (dest == null)
                return;
            CopyTo (dest, index);
        }

        public void CopyTo (T[] array, int index)
        {
            IEnumerator<T> e = InternalGetEnumerator ();
            int i = index;
            while (e.MoveNext ()) {
                array [i++] = e.Current;
            }
        }

        public T[] ToArray ()
        {
            T[] dest = new T [count];
            CopyTo (dest, 0);
            return dest;
        }

        bool ICollection.IsSynchronized {
            get { return true; }
        }

        bool IProducerConsumerCollection<T>.TryTake (out T item)
        {
            return TryDequeue (out item);
        }

        object syncRoot = new object();
        object ICollection.SyncRoot {
            get { return syncRoot; }
        }

        public int Count {
            get {
                return count;
            }
        }

        public bool IsEmpty {
            get {
                return count == 0;
            }
        }
    }
}
