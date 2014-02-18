using System;

namespace Exceptionless {
    public interface IJsonSerializer {
        string Serialize(object model, string[] exclusions = null);
        T Deserialize<T>(string json);
    }
}
