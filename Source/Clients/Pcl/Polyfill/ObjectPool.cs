#if PFX_LEGACY_3_5 || PORTABLE40

using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace System.Collections.Concurrent
{
    internal abstract class ObjectPool<T> where T : class
    {
        const int capacity = 20;
        const int bit = 0x8000000;

        readonly T[] buffer;
        int addIndex;
        int removeIndex;

        public ObjectPool ()
        {
            buffer = new T[capacity];
            for (int i = 0; i < capacity; i++)
                buffer[i] = Creator ();
            addIndex = capacity - 1;
        }

        protected abstract T Creator ();

        public T Take ()
        {
            if ((addIndex & ~bit) - 1 == removeIndex)
                return Creator ();

            int i;
            T result;
            int tries = 3;

            do {
                i = removeIndex;
                if ((addIndex & ~bit) - 1 == i || tries == 0)
                    return Creator ();
                result = buffer[i % capacity];
            } while (Interlocked.CompareExchange (ref removeIndex, i + 1, i) != i && --tries > -1);

            return result;
        }

        public void Release (T obj)
        {
            if (obj == null || addIndex - removeIndex >= capacity - 1)
                return;

            int i;
            int tries = 3;
            do {
                do {
                    i = addIndex;
                } while ((i & bit) > 0);
                if (i - removeIndex >= capacity - 1)
                    return;
            } while (Interlocked.CompareExchange (ref addIndex, i + 1 + bit, i) != i && --tries > 0);

            buffer[i % capacity] = obj;
            addIndex = addIndex - bit;
        }
    }
}

#endif