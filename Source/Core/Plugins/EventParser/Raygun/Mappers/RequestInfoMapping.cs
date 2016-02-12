using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class RequestInfoMapping {
        public static RequestInfo Map(RaygunModel model) {
            var request = model?.Details?.Request;
            if (request == null)
                return null;

            var environment = model.Details?.Environment;
            var ri = new RequestInfo {
                ClientIpAddress = request.IPAddress,
                //Cookies
                Host = request.HostName,
                HttpMethod = request.HttpMethod,
                IsSecure =  request.HostName?.StartsWith("https") ?? false,
                Path = request.Url ?? request.GetHeaderValue("PATH_INFO"),
                PostData = request.Form.Any() ? request.Form : null,
                Referrer = request.GetHeaderValue("Referer") ?? request.GetHeaderValue("HTTP_REFERER"),
                UserAgent = request.GetHeaderValue("HTTP_USER_AGENT") ?? request.GetHeaderValue("USER-AGENT") ?? environment.BrowserVersion,
                QueryString = request.QueryString as Dictionary<string, string>
            };
            
            int port;
            Uri uri;
            if (Int32.TryParse(request.GetHeaderValue("SERVER_PORT"), out port))
                ri.Port = port;
            else if (Uri.TryCreate(request.HostName, UriKind.RelativeOrAbsolute, out uri))
                ri.Port = uri.Port;
            else
                ri.Port = 80;

            if (request.Headers != null)
                ri.Data[nameof(request.Headers)] = request.Headers;

            if (!String.IsNullOrEmpty(request.RawData))
                ri.Data[nameof(request.RawData)] = request.RawData;
            
            // TODO: [Discussion] Should we bring in our own user agent parser and parse the user agent or rely on there stuff?
            if (!String.IsNullOrEmpty(environment?.Browser))
                ri.Data[RequestInfo.KnownDataKeys.Browser] = environment.Browser;
            if (!String.IsNullOrEmpty(environment?.BrowserVersion))
                ri.Data[RequestInfo.KnownDataKeys.BrowserVersion] = environment.BrowserVersion;
            if (environment?.BrowserHeight > 0)
                ri.Data[nameof(environment.BrowserHeight)] = environment.BrowserHeight;
            if (environment?.BrowserWidth > 0)
                ri.Data[nameof(environment.BrowserWidth)] = environment.BrowserWidth;
            
            if (!String.IsNullOrEmpty(environment?.DeviceName)) {
                if (!String.IsNullOrEmpty(environment.DeviceManufacturer))
                    ri.Data[RequestInfo.KnownDataKeys.Device] = $"{environment.DeviceManufacturer} {environment.DeviceName}";
                else
                    ri.Data[RequestInfo.KnownDataKeys.Device] = environment.DeviceName;
            }

            return ri;
        }

        // NOTE: I don't see cookies anywhere in any of there api specs..
        //private static Dictionary<string, string> MapCookies(IList cookies) {
        //    if (cookies == null) {
        //        return null;
        //    }

        //    var dictionary = new Dictionary<string, string>();

        //    foreach (var cookie in cookies) {
        //        var raygunCookie = ((JObject)cookie).ToObject<Cookie>();

        //        dictionary.Add(raygunCookie.Name, raygunCookie.Value);
        //    }

        //    return dictionary;
        //}
    }
}
