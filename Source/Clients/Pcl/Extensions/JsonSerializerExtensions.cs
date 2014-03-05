using System;

namespace Exceptionless.Extensions {
    public static class JsonSerializerExtensions {
        public static T Deserialize<T>(this IJsonSerializer serializer, string json) {
            return (T)serializer.Deserialize(json, typeof(T));
        }
    }
}
