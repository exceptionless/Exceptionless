using System;

namespace Exceptionless {
    public interface IJsonSerializer {
        string Serialize(object model, string[] exclusions = null, int maxDepth = 5, bool continueOnSerializationError = true);
        object Deserialize(string json, Type type);
    }
}