using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Serializer {
    internal class RequestInfoConverter : CustomCreationConverter<RequestInfo> {
        public override RequestInfo Create(Type objectType) {
            return new RequestInfo();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            object obj = base.ReadJson(reader, objectType, existingValue, serializer);
            var result = obj as RequestInfo;
            if (result == null)
                return obj;

            if (result.PostData is JObject)
                result.PostData = result.PostData.ToString();

            return result;
        }
    }
}