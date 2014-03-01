using System;

namespace Exceptionless {
    public interface IJsonSerializer {
        string Serialize(object model, string[] exclusions = null);
        object Deserialize(string json, Type type);
    }
}
