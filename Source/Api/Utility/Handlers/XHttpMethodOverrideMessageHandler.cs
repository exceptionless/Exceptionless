using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Routing;

namespace Exceptionless.Api.Utility {
    public class XHttpMethodOverrideDelegatingHandler : DelegatingHandler {
        private static readonly string[] _httpMethods = { "PUT", "HEAD", "DELETE", "PATCH" };
        private const string HTTP_METHOD_OVERRIDE_HEADER = "X-HTTP-Method-Override";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.Method == HttpMethod.Post && request.Headers.Contains(HTTP_METHOD_OVERRIDE_HEADER)) {
                string httpMethod = request.Headers.GetValues(HTTP_METHOD_OVERRIDE_HEADER).FirstOrDefault();
                if (_httpMethods.Contains(httpMethod, StringComparer.InvariantCultureIgnoreCase))
                    request.Method = new HttpMethod(httpMethod);
            }

            object value;
            if (request.Properties.TryGetValue("MS_HttpRouteData", out value)) {
                var routeData = value as IHttpRouteData;
                if (routeData != null) {
                    if (!routeData.Values.ContainsKey("action"))
                        routeData.Values["action"] = request.Method.ToString();
                }
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}