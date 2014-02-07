using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Text;

namespace CodeSmith.Core.Helpers.SelfSerializerStrategy
{
    internal class XmlSerializationStrategy<T> : ISerializationStrategy<T>
    {
        public Stream Serialize(Stream s, T t)
        { 
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            serializer.Serialize(s, t);
            return s;
        }

        public T Deserialize(Stream s)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            return (T)serializer.Deserialize(s);
        }
    }
}
