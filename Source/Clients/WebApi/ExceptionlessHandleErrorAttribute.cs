#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Filters;
#if !EMBEDDED
using CodeSmith.Core.Component;

#else
using Exceptionless.Utility;
#endif

namespace Exceptionless.WebApi {
    public class ExceptionlessHandleErrorAttribute : IExceptionFilter {
        public bool HasWrappedFilter { get { return WrappedFilter != null; } }

        public IExceptionFilter WrappedFilter { get; set; }
        public bool AllowMultiple { get { return HasWrappedFilter && WrappedFilter.AllowMultiple; } }

        public virtual void OnHttpException(HttpActionExecutedContext actionExecutedContext, CancellationToken cancellationToken) {
            if (HasWrappedFilter)
                WrappedFilter.ExecuteExceptionFilterAsync(actionExecutedContext, cancellationToken);

            var contextData = new Dictionary<string, object> {
                { "HttpActionContext", actionExecutedContext.ActionContext }
            };
            ExceptionlessClient.Current.ProcessUnhandledException(actionExecutedContext.Exception, "ExceptionHttpFilter", true, contextData);
        }

        public Task ExecuteExceptionFilterAsync(HttpActionExecutedContext actionExecutedContext, CancellationToken cancellationToken) {
            ExceptionlessClient.Current.Log.Trace("ExecuteExceptionFilterAsync executing...");
            if (actionExecutedContext == null)
                throw new ArgumentNullException("actionExecutedContext");

            OnHttpException(actionExecutedContext, cancellationToken);
            return TaskHelper.Completed();
        }
    }
}