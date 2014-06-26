using System;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Security.OAuth;

namespace Exceptionless.Api {
    public class ExceptionlessOAuthBearerAuthenticationProvider : OAuthBearerAuthenticationProvider {
        public override Task RequestToken(OAuthRequestTokenContext context) {
            if (!String.IsNullOrEmpty(context.Token))
                return Task.FromResult(0);

            context.Token = context.Request.Query.Get("access_token");
            if (!String.IsNullOrEmpty(context.Token))
                return Task.FromResult(0);


            var authHeader = context.Request.Headers.Get("Authorization");
            if (String.IsNullOrEmpty(authHeader))
                return Task.FromResult(0);

            var authHeaderVal = AuthenticationHeaderValue.Parse(authHeader);
            if (authHeaderVal.Scheme.Equals("token", StringComparison.OrdinalIgnoreCase)) {
                context.Token = authHeaderVal.Parameter;
            } else if (authHeaderVal.Scheme.Equals("basic", StringComparison.OrdinalIgnoreCase)) {
                string text = Encoding.UTF8.GetString(Convert.FromBase64String(authHeaderVal.Parameter));
                int delimiterIndex = text.IndexOf(':');
                if (delimiterIndex >= 0 && String.Equals("client", text.Substring(0, delimiterIndex), StringComparison.OrdinalIgnoreCase))
                    context.Token = text.Substring(delimiterIndex + 1);
            }

            return Task.FromResult(0);
        }
    }
}