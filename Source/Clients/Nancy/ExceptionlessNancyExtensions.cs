#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.ExtendedData;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Nancy;
using Nancy.Bootstrapper;
using Nancy;

namespace Exceptionless
{
    public static class ExceptionlessNancyExtensions
    {
        public static void RegisterNancy(this ExceptionlessClient client, IPipelines pipelines)
        {
            client.RegisterPlugin(new ExceptionlessNancyPlugin());
            client.Startup();
            client.Configuration.IncludePrivateInformation = true;
            pipelines.OnError += (ctx, exception) =>
            {
                var ctxData = new Dictionary<string, object>();
                ctxData.Add("NancyContext", ctx);
                var error = client.CreateError(exception, submissionMethod: "Unhandled", contextData: ctxData);
                error.AddRequestInfo(ctx);
                client.SubmitError(error);
                return ctx.Response;
            };
        }

        public static void UnregisterNancy(this ExceptionlessClient client)
        {
            client.UnregisterPlugin(typeof(ExceptionlessNancyPlugin).FullName);
            client.Shutdown();
        }

        public static Error AddRequestInfo(this Error error, NancyContext context)
        {
            if (context == null)
            {
                return error;
            }
            error.RequestInfo = NancyRequestInfoCollector.Collect(context, ExceptionlessClient.Current);
            error.ExceptionlessClientInfo.Platform = "Nancy";
            return error;
        }

        public static ErrorBuilder AddRequestInfo(this ErrorBuilder builder, NancyContext context)
        {
            if (context == null || builder.Target == null)
            {
                return builder;
            }
            builder.Target.AddRequestInfo(context);
            return builder;
        }

        internal static NancyContext GetHttpActionContext(this IDictionary<string, object> data)
        {
            if (!data.HasNancyContext())
            {
                return null;
            }

            return data["NancyContext"] as NancyContext;
        }

        internal static bool HasNancyContext(this IDictionary<string, object> data)
        {
            return data.ContainsKey("NancyContext");
        }
    }
}