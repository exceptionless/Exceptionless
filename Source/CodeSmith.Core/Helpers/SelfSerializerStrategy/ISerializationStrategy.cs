using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodeSmith.Core.Helpers.SelfSerializerStrategy
{
    internal interface ISerializationStrategy<T>
    {
        System.IO.Stream Serialize( Stream s, T type);
        T Deserialize(System.IO.Stream stream);
    }
}
