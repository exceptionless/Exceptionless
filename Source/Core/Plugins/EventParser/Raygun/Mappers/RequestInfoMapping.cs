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
