using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class RequestInfoMapping {
        public static RequestInfo Map(RaygunModel raygunModel) {
            var raygunRequest = raygunModel?.Details?.Request;

            if (raygunRequest == null) {
                return null;
            }

            var requestInfo = new RequestInfo();

            requestInfo.ClientIpAddress = raygunRequest.IPAddress;
            requestInfo.Host = raygunRequest.HostName;
            requestInfo.HttpMethod = raygunRequest.HttpMethod;
            requestInfo.Cookies = MapCookies(raygunRequest.Cookies);
            requestInfo.Referrer = raygunRequest.GetDataValue("HTTP_REFERER");
            requestInfo.UserAgent = raygunRequest.GetDataValue("HTTP_USER_AGENT") ?? raygunRequest.GetHeaderValue("USER-AGENT");
            requestInfo.Path = raygunRequest.GetDataValue("PATH_INFO");
            requestInfo.QueryString = raygunRequest.QueryString as Dictionary<string, string>;

            var serverPort = raygunRequest.GetDataValue("SERVER_PORT");

            if (serverPort != null) {
                requestInfo.Port = int.Parse(serverPort);
            }

            var https = raygunRequest.GetDataValue("HTTPS");

            if (https != null && https.ToUpperInvariant() == "ON") {
                requestInfo.IsSecure = true;
            }
            
            //requestInfo.Data;
            requestInfo.PostData = raygunRequest.Form;  

            return requestInfo;
        }

        private static Dictionary<string, string> MapCookies(IList cookies) {
            if (cookies == null) {
                return null;
            }

            var dictionary = new Dictionary<string, string>();

            foreach (var cookie in cookies) {
                var raygunCookie = ((JObject)cookie).ToObject<Cookie>();

                dictionary.Add(raygunCookie.Name, raygunCookie.Value);
            }

            return dictionary;
        }
    }
}
