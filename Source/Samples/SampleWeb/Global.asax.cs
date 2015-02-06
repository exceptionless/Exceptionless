#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web;

namespace Exceptionless.SampleWeb {
    public partial class Global : HttpApplication {
        protected void Application_Start(object sender, EventArgs e) {
            ExceptionlessClient.Default.Configuration.UseTraceLogger();
            ExceptionlessClient.Default.Configuration.UseReferenceIds();
            ExceptionlessClient.Default.SubmittingEvent += OnSubmittingEvent;
        }

        private void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
            // you can get access to the report here
            e.Event.Tags.Add("WebTag");
        }
    }
}