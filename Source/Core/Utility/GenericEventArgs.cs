using System;

namespace Exceptionless.Core.Utility
{
    [Serializable]
    public class EventArgs<T> : EventArgs
    {
        public EventArgs(T value)
        {
            Value = value;
        }

        public T Value { get; private set; }
    }

    [Serializable]
    public class EventArgs<T1, T2> : EventArgs<T1>
    {
        public EventArgs(T1 value1, T2 value2) : base(value1)
        {
            Value2 = value2;
        }

        public T2 Value2 { get; private set; }
    }

    [Serializable]
    public class EventArgs<T1, T2, T3> : EventArgs<T1, T2>
    {
        public EventArgs(T1 value1, T2 value2, T3 value3) : base(value1, value2)
        {
            Value3 = value3;
        }

        public T3 Value3 { get; private set; }
    }
}
