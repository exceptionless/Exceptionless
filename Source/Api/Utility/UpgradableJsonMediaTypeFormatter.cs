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
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Migrations.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Api.Utility {
    public class UpgradableJsonMediaTypeFormatter : JsonMediaTypeFormatter {
        public UpgradableJsonMediaTypeFormatter() {
            LegacyErrorDocumentUpgrader.RegisterUpgrades();
        }

        public override Task<object> ReadFromStreamAsync(Type type, Stream readStream, HttpContent content, IFormatterLogger formatterLogger) {
            HttpContentHeaders contentHeaders = content == null ? null : content.Headers;
            if (!DocumentUpgrader.Current.CanUpgradeType(type) || (contentHeaders != null && contentHeaders.ContentLength == 0))
                return base.ReadFromStreamAsync(type, readStream, content, formatterLogger);

            Task<object> task = Task<object>.Factory.StartNew(() => {
                Encoding effectiveEncoding = SelectCharacterEncoding(contentHeaders);
                try {
                    using (var reader = new StreamReader(readStream, effectiveEncoding)) {
                        string json = reader.ReadToEnd();

                        try {
                            if (Settings.Current.SaveIncomingErrorsToDisk)
                                File.WriteAllText(String.Format("{0}\\{1}.json", Settings.Current.IncomingErrorPath, Guid.NewGuid().ToString("N")), json);
                        } catch (Exception ex) {
                            if (formatterLogger != null)
                                formatterLogger.LogError(String.Empty, ex);
                        }

                        try {
                            JObject document = JObject.Parse(json);
                            DocumentUpgrader.Current.Upgrade(document, type);
                            return JsonConvert.DeserializeObject(document.ToString(), type);
                        } catch (Exception ex) {
                            ex.ToExceptionless().AddObject(json, "Error").Submit();
                            throw new Exception("An exception occurred while upgrading the document.", ex);
                        }
                    }
                } catch (Exception ex) {
                    if (formatterLogger == null)
                        throw;

                    formatterLogger.LogError(String.Empty, ex);
                    return GetDefaultValueForType(type);
                }
            });

            return task;
        }
    }
}