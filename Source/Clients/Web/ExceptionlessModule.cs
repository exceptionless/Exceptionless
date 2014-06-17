#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Web;
using Exceptionless.Dependency;
using Exceptionless.Web.Extensions;

namespace Exceptionless.Web {
    public class ExceptionlessModule : IHttpModule {
        private HttpApplication _app;

        public virtual void Init(HttpApplication app) {
            ExceptionlessClient.Default.Startup();
            ExceptionlessClient.Default.RegisterHttpApplicationErrorHandler(app);
            ExceptionlessClient.Default.Configuration.IncludePrivateInformation = true;
            ExceptionlessClient.Default.Configuration.AddEnrichment<ExceptionlessWebEnrichment>();
            ExceptionlessClient.Default.Configuration.Resolver.Register<ILastReferenceIdManager, WebLastReferenceIdManager>();
            
            _app = app;
        }

        public void Dispose() {
            ExceptionlessClient.Default.Shutdown();
            ExceptionlessClient.Default.UnregisterHttpApplicationErrorExceptionHandler(_app);
        }
    }
}