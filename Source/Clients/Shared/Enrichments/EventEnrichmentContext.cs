#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Dependency;
using Exceptionless.Logging;

namespace Exceptionless.Enrichments {
    public class EventEnrichmentContext {
        public EventEnrichmentContext(ExceptionlessClient client, ContextData contextData = null) {
            Client = client;
            Data = contextData ?? new ContextData();
        }

        public ExceptionlessClient Client { get; private set; }
        public IDependencyResolver Resolver { get { return Client.Configuration.Resolver; }}
        public ContextData Data { get; private set; }

        public IExceptionlessLog Log {
            get { return Resolver.GetLog(); }
        }
    }
}