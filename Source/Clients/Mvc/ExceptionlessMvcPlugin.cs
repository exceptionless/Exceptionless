#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Models;
using Exceptionless.Plugins;
using Exceptionless.Web;

namespace Exceptionless.Mvc {
    internal class ExceptionlessMvcPlugin : ExceptionlessWebPlugin {
        public override void AfterCreated(ExceptionlessPluginContext context, Error error, Exception exception) {
            base.AfterCreated(context, error, exception);

            if (context.Data.GetHttpContext() == null)
                return;

            error.ExceptionlessClientInfo.Platform = ".NET MVC";
        }
    }
}