#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Web.Http.Controllers;
using Exceptionless.Enrichments;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.WebApi {
    internal class ExceptionlessWebApiEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (!context.Data.ContainsKey("HttpActionContext"))
                return;

            HttpActionContext actionContext = context.Data.GetHttpActionContext();
            if (actionContext == null)
                return;

            IPrincipal principal = GetPrincipal(actionContext.Request);
            //if (context.Client.Configuration.IncludePrivateInformation
            //    && principal != null && principal.Identity.IsAuthenticated)
            //    error.UserName = principal.Identity.Name;

            try {
                ev.AddHttpRequestInfo(actionContext);
            } catch (Exception ex) {
                context.Log.Error(typeof(ExceptionlessWebApiEnrichment), ex, "Error adding request info.");
            }
        }

        private static IPrincipal GetPrincipal(HttpRequestMessage request) {
            if (request == null)
                throw new ArgumentNullException("request");

            const string RequestContextKey = "MS_RequestContext";

            object context;
            if (!request.Properties.TryGetValue(RequestContextKey, out context) || context == null)
                return Thread.CurrentPrincipal;

            if (_principalGetAccessor == null) {
                PropertyInfo principalProperty = context.GetType().GetProperties().SingleOrDefault(obj => obj.Name == "Principal");
                if (principalProperty == null)
                    return Thread.CurrentPrincipal;

                _principalGetAccessor = BuildGetAccessor(principalProperty.GetGetMethod());
            }

            var principal = _principalGetAccessor(context) as IPrincipal;

            return principal ?? Thread.CurrentPrincipal;
        }

        private static Func<object, object> _principalGetAccessor;

        private static Func<object, object> BuildGetAccessor(MethodInfo method) {
            ParameterExpression obj = Expression.Parameter(typeof(object), "o");

            Expression<Func<object, object>> expr = Expression.Lambda<Func<object, object>>(
                Expression.Convert(Expression.Call(Expression.Convert(obj, method.DeclaringType), method), typeof(object)), obj);

            return expr.Compile();
        }
    }
}