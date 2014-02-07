#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Migrations.Documents {
    public static class ErrorDocumentUpgrader {
        public static void RegisterUpgrades() {
            // TODO We don't know the specific revision number as team city doesn't go back that far. But revision 529 was on the 2/13/2013.
            DocumentUpgrader.Current.Add<Error>(500, document => {
                // Revision: 19e5477ca7cea0ebcf7492822b972dbc7df29c88 on 2/8/2013 11:25:48 PM
                int build = GetBuildNumber(document);
                if (build > 500)
                    return;

                var clientInfo = document["ExceptionlessClientInfo"] as JObject;

                // Changed type of InstallDate from DateTime to DateTimeOffset
                if (clientInfo != null && clientInfo.HasValues && clientInfo["InstallDate"] != null) {
                    DateTimeOffset date; // This shouldn't hurt using DateTimeOffset to try and parse a date. It insures you won't lose any info.
                    if (DateTimeOffset.TryParse(clientInfo["InstallDate"].ToString(), out date)) {
                        clientInfo.Remove("InstallDate");
                        clientInfo.Add("InstallDate", new JValue(date));
                    } else
                        clientInfo.Remove("InstallDate");
                }
            });

            DocumentUpgrader.Current.Add<Error>(844, document => {
                int build = GetBuildNumber(document);
                if (build > 844)
                    return;

                var requestInfo = document["RequestInfo"] as JObject;

                // Changed type of InstallDate from DateTime to DateTimeOffset
                if (requestInfo == null || !requestInfo.HasValues)
                    return;

                if (requestInfo["Cookies"] != null && requestInfo["Cookies"].HasValues) {
                    var cookies = requestInfo["Cookies"] as JObject;
                    if (cookies != null)
                        cookies.Remove("");
                }

                if (requestInfo["Form"] != null && requestInfo["Form"].HasValues) {
                    var form = requestInfo["Form"] as JObject;
                    if (form != null)
                        form.Remove("");
                }

                if (requestInfo["QueryString"] != null && requestInfo["QueryString"].HasValues) {
                    var queryString = requestInfo["QueryString"] as JObject;
                    if (queryString != null)
                        queryString.Remove("");
                }
            });

            DocumentUpgrader.Current.Add<Error>(850, document => {
                int build = GetBuildNumber(document);
                if (build > 850)
                    return;

                JObject current = document;
                while (current != null) {
                    var extendedData = document["ExtendedData"] as JObject;
                    if (extendedData != null) {
                        if (extendedData["ExtraExceptionProperties"] != null)
                            extendedData.Rename("ExtraExceptionProperties", ExtendedDataDictionary.EXCEPTION_INFO_KEY);

                        if (extendedData["ExceptionInfo"] != null)
                            extendedData.Rename("ExceptionInfo", ExtendedDataDictionary.EXCEPTION_INFO_KEY);

                        if (extendedData["TraceInfo"] != null)
                            extendedData.Rename("TraceInfo", ExtendedDataDictionary.TRACE_LOG_KEY);
                    }

                    current = current["Inner"] as JObject;
                }
            });
        }

        private static int GetBuildNumber(JObject document) {
            if (!document.HasValues)
                return 0;

            var clientInfo = document["ExceptionlessClientInfo"] as JObject;
            if (clientInfo == null || !clientInfo.HasValues || clientInfo["Version"] == null)
                return 0;

            if (clientInfo["Version"].ToString().Contains(" ")) {
                string version = clientInfo["Version"].ToString().Split(' ').First();
                return new Version(version).Build;
            }

            if (clientInfo["Version"].ToString().Contains("-")) {
                string version = clientInfo["Version"].ToString().Split('-').First();
                return new Version(version).Build;
            }

            // old version format
            return new Version(clientInfo["Version"].ToString()).Revision;
        }
    }
}