#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless.Plugins {
    public class ExceptionlessPlugin : IExceptionlessPlugin {
        public virtual void AfterCreated(ExceptionlessPluginContext context, Error error, Exception exception) {}

        public virtual void AddDefaultInformation(ExceptionlessPluginContext context, Error error) {}

        public virtual bool SupportsShowingUnhandledErrorSubmissionUI { get { return false; } }

        public virtual bool ShowUnhandledErrorSubmissionUI(ExceptionlessPluginContext context, Error error) {
            return true;
        }
    }
}