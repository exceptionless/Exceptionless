#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.TinyIoc;

namespace Exceptionless.SampleNancy {
    public class ExceptionlessBootstrapper : DefaultNancyBootstrapper {
        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines) {
            base.ApplicationStartup(container, pipelines);

            ExceptionlessClient.Default.Configuration.UseTraceLogger();
            ExceptionlessClient.Default.RegisterNancy(pipelines);
        }
    }
}
