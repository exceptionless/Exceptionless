#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.IO;
using System.Web;
using System.Web.UI;

namespace Exceptionless.SampleWeb {
    public partial class _Default : Page {
        protected void Page_Load(object sender, EventArgs e) {
            Response.Cookies.Add(new HttpCookie("Blah", "blah"));
            //ExceptionlessClient.Default.UpdateConfiguration(true);
            ExceptionlessClient.Default.Configuration.DefaultTags.Add("Blah");
            Trace.Write("Default.aspx load");
        }

        protected void ErrorButton_Click(object sender, EventArgs e) {
            string text = File.ReadAllText("blah.txt");
        }
    }
}