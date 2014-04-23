#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Dependency;

namespace Exceptionless.Enrichments {
    public class EventEnrichmentContext {
        public EventEnrichmentContext(ExceptionlessClient client, IEnumerable<KeyValuePair<string, object>> contextData = null) {
            Client = client;
            ContextData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (contextData == null)
                return;

            foreach (var kvp in contextData)
                ContextData.Add(kvp);
        }

        public ExceptionlessClient Client { get; private set; }
        public IDependencyResolver Resolver { get { return Client.Configuration.Resolver; }}
        public IDictionary<string, object> ContextData { get; private set; }

        public static class KnownContextDataKeys {
            public const string Exception = "@__Exception";
        }
    }
}