#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Plugins;
using Nancy;

namespace Exceptionless.Nancy {
    internal class ExceptionlessNancyPlugin : ExceptionlessPlugin {
        public override void AfterCreated(ExceptionlessPluginContext context, Error error, Exception exception) {
            base.AfterCreated(context, error, exception);

            error.ExceptionlessClientInfo.Platform = "Nancy";
        }

        public override void AddDefaultInformation(ExceptionlessPluginContext context, Error error) {
            base.AddDefaultInformation(context, error);

            NancyContext nancyContext = context.Data.GetNancyContext();
            if (nancyContext == null)
                return;

            try {
                error.AddRequestInfo(nancyContext);

                if (nancyContext.CurrentUser != null && context.Client.Configuration.IncludePrivateInformation)
                    error.UserName = nancyContext.CurrentUser.UserName;
            } catch (Exception ex) {
                context.Client.Log.Error(typeof(ExceptionlessNancyPlugin), ex, "Error adding request info.");
            }
        }
    }
}