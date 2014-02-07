using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace CodeSmith.Core.Helpers.SelfSerializerStrategy
{
    internal class BinarySerializationStrategy<T> : ISerializationStrategy<T>
    {
        public Stream Serialize(Stream s, T t)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(s, t);
            return s;
        }

        public T Deserialize(Stream stream)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            return (T)formatter.Deserialize(stream);
        }
    }
}
