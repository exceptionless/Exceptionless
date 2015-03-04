using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Exceptionless.Core;

namespace Exceptionless.Api.Security {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class RequireHttpsAttribute : FilterAttribute, IAuthorizationFilter {
        protected virtual void HandleNonHttpsRequest(HttpActionContext context) {
            string url = String.Format("https://{0}{1}", context.Request.RequestUri.Host, context.Request.RequestUri.PathAndQuery);

            HttpResponseMessage response = context.ControllerContext.Request.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri(url);

            context.Response = response;
        }

        async Task<HttpResponseMessage> IAuthorizationFilter.ExecuteAuthorizationFilterAsync(HttpActionContext actionContext, CancellationToken cancellationToken, Func<Task<HttpResponseMessage>> continuation) {
            if (actionContext == null)
                throw new ArgumentNullException("actionContext");

            if (continuation == null)
                throw new ArgumentNullException("continuation");

            if (Settings.Current.EnableSSL && actionContext.Request.RequestUri.Scheme != Uri.UriSchemeHttps)
                HandleNonHttpsRequest(actionContext);

            if (actionContext.Response != null)
                return actionContext.Response;

            return await continuation();
        }

        bool IFilter.AllowMultiple { get { return true; } }
    }
}