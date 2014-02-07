#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Json;
using Exceptionless.Json.Converters;
using Exceptionless.Json.Linq;
using Exceptionless.Models;

namespace Exceptionless.Serialization {
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