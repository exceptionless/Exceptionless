#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Web;
using Exceptionless.Dependency;
using Exceptionless.Enrichments;

namespace Exceptionless.Web {
    public class ExceptionlessModule : IHttpModule {
        private HttpApplication _context;

        public void Dispose() {
            //ExceptionlessClient.Default.Shutdown();
            _context.Error -= OnError;
        }

        public virtual void Init(HttpApplication context) {
            ExceptionlessClient.Default.Configuration.Resolver.Register<ILastReferenceIdManager, WebLastReferenceIdManager>();
            ExceptionlessClient.Default.Configuration.AddEnrichment<ExceptionlessWebEnrichment>();
            //ExceptionlessClient.Default.Startup();
            ExceptionlessClient.Default.Configuration.IncludePrivateInformation = true;
            _context = context;
            _context.Error += OnError;
        }

        private void OnError(object sender, EventArgs e) {
            if (HttpContext.Current == null)
                return;

            Exception exception = HttpContext.Current.Server.GetLastError();
            if (exception == null)
                return;

            var contextData = new ContextData();
            contextData.SetUnhandled();
            contextData.SetSubmissionMethod("HttpApplicationError");
            contextData.Add("HttpContext", HttpContext.Current.ToWrapped());

            exception.ToExceptionless(contextData).Submit();
        }
    }
}