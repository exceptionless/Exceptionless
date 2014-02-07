using System;
using System.IO;
using CodeSmith.Core.Helpers.SelfSerializerStrategy;

namespace CodeSmith.Core.Helpers
{
    public class SelfSerializer<T>
    {
        public Stream BinarySerialize(T t)
        {
            return BinarySerializer(t, new MemoryStream());
        }

        public Stream BinarySerializer(T t, Stream s)
        {
            ISerializationStrategy<T> strategy = new BinarySerializationStrategy<T>();
            return strategy.Serialize(s, t);
        }

        public T BinaryDeserialize(Stream stream)
        {
            ISerializationStrategy<T> strategy = new BinarySerializationStrategy<T>();
            return strategy.Deserialize(stream);
        }

        public Stream XmlSerialize(T t)
        {
            return XmlSerialize(t, new MemoryStream());
        }

        public Stream XmlSerialize(T t, Stream s)
        {
            ISerializationStrategy<T> strategy = new XmlSerializationStrategy<T>();
            return strategy.Serialize(s,t);
        }

        public T XmlDeserialize(Stream stream)
        {
            ISerializationStrategy<T> strategy = new XmlSerializationStrategy<T>();
            return strategy.Deserialize(stream);
        }

        #region Singleton

        public static SelfSerializer<T> Current
        {
            get { return Nested.Current; }
        }

        private class Nested
        {
            static Nested()
            { }

            internal readonly static SelfSerializer<T> Current = new SelfSerializer<T>();
        }

        #endregion
    }
}
