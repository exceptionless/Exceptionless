#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Web;

namespace Exceptionless.Web {
    public class ExceptionlessModule : IHttpModule {
        private HttpApplication _context;

        public void Dispose() {
            ExceptionlessClient.Current.Shutdown();
            _context.Error -= OnError;
        }

        public virtual void Init(HttpApplication context) {
            ExceptionlessClient.Current.LastErrorIdManager = new WebLastErrorIdManager(ExceptionlessClient.Current);
            ExceptionlessClient.Current.RegisterPlugin(new ExceptionlessWebPlugin());
            ExceptionlessClient.Current.Startup();
            ExceptionlessClient.Current.Configuration.IncludePrivateInformation = true;
            _context = context;
            _context.Error += OnError;
        }

        private void OnError(object sender, EventArgs e) {
            if (HttpContext.Current == null)
                return;

            Exception exception = HttpContext.Current.Server.GetLastError();
            if (exception == null)
                return;

            ExceptionlessClient.Current.ProcessUnhandledException(exception, "HttpApplicationError", true, HttpContext.Current.ToDictionary());
        }
    }
}