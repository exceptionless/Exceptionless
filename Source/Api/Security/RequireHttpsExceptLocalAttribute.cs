using System;
using System.Web.Http.Controllers;

namespace Exceptionless.Api.Security {
    public class RequireHttpsExceptLocalAttribute : RequireHttpsAttribute {
        protected override void HandleNonHttpsRequest(HttpActionContext context) {
            if (context == null)
                throw new ArgumentNullException("context");

            if (HostIsLocal(context.Request.RequestUri.Host))
                return;

            base.HandleNonHttpsRequest(context);
        }

        private bool HostIsLocal(string hostName) {
            return hostName.Contains("localtest.me") || hostName.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }
    }
}