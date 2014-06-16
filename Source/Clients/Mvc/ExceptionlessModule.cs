#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Exceptionless.Mvc {
    public class ExceptionlessModule : IHttpModule {
        private HttpApplication _context;

        public void Dispose() {
            ExceptionlessClient.Default.Shutdown();
            _context.Error -= OnError;
        }

        public virtual void Init(HttpApplication context) {
            ExceptionlessClient.Default.LastErrorIdManager = new WebLastErrorIdManager(ExceptionlessClient.Default);
            ExceptionlessClient.Default.RegisterPlugin(new ExceptionlessMvcPlugin());
            ExceptionlessClient.Default.Startup();
            ExceptionlessClient.Default.Configuration.IncludePrivateInformation = true;
            _context = context;
            _context.Error -= OnError;
            _context.Error += OnError;

            if (!GlobalFilters.Filters.Any(f => f.Instance is ExceptionlessSendErrorsAttribute))
                GlobalFilters.Filters.Add(new ExceptionlessSendErrorsAttribute());
        }

        private void OnError(object sender, EventArgs e) {
            if (HttpContext.Current == null)
                return;

            Exception exception = HttpContext.Current.Server.GetLastError();
            if (exception == null)
                return;

            ExceptionlessClient.Default.ProcessUnhandledException(exception, "HttpApplicationError", true, HttpContext.Current.ToDictionary());
        }
    }
}