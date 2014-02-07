#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Reflection;
using Exceptionless.Json;
using Exceptionless.Json.Serialization;

namespace Exceptionless.Serialization {
    internal class ConditionalContractResolver : DefaultContractResolver {
        private readonly Func<JsonProperty, bool> _includeProperty;

        public ConditionalContractResolver(Func<JsonProperty, bool> includeProperty) {
            _includeProperty = includeProperty;
        }

        protected override JsonProperty CreateProperty(
            MemberInfo member, MemberSerialization memberSerialization) {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            Predicate<object> shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => _includeProperty(property) && (shouldSerialize == null || shouldSerialize(obj));
            return property;
        }
    }
}