using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class RequestInfoMapping {
        public static RequestInfo Map(RaygunModel model) {
            var request = model?.Details?.Request;
            if (request == null)
                return null;

            var ri = new RequestInfo {
                ClientIpAddress = request.IPAddress,
                //Cookies
                Host = request.HostName,
                HttpMethod = request.HttpMethod,
                IsSecure =  request.HostName?.StartsWith("https") ?? false,
                Path = request.GetHeaderValue("PATH_INFO"),
                PostData = request.Form.Any() ? request.Form : null,
                Referrer = request.GetHeaderValue("Referer") ?? request.GetHeaderValue("HTTP_REFERER"),
                //Url
                UserAgent = request.GetHeaderValue("HTTP_USER_AGENT") ?? request.GetHeaderValue("USER-AGENT"),
                QueryString = request.QueryString as Dictionary<string, string>
            };
            
            int port;
            if (Int32.TryParse(request.GetHeaderValue("SERVER_PORT"), out port))
                ri.Port = port;

            if (!String.IsNullOrEmpty(request.RawData))
                ri.Data.Add("RawData", request.RawData);
            
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
