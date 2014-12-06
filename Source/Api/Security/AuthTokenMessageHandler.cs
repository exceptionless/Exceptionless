using System;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;

namespace Exceptionless.Api.Security {
    public class AuthTokenMessageHandler : DelegatingHandler {
        public const string BearerScheme = "Bearer";
        public const string BasicScheme = "Basic";
        public const string TokenScheme = "Token";

        private readonly TokenManager _tokenManager;

        public AuthTokenMessageHandler(TokenManager tokenManager) {
            _tokenManager = tokenManager;
        }

        protected virtual Task<HttpResponseMessage> BaseSendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return base.SendAsync(request, cancellationToken);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var authHeader = request.Headers.Authorization;
            string token = null;
            if (authHeader != null && (authHeader.Scheme == BearerScheme || authHeader.Scheme == TokenScheme))
                token = authHeader.Parameter;
            else if (authHeader != null && authHeader.Scheme == BasicScheme) {
                string text = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter));
                int delimiterIndex = text.IndexOf(':');
                if (delimiterIndex >= 0 && String.Equals("client", text.Substring(0, delimiterIndex), StringComparison.OrdinalIgnoreCase))
                    token = text.Substring(delimiterIndex + 1);
            } else {
                token = request.GetQueryString("access_token");
            }

            if (String.IsNullOrEmpty(token))
                return BaseSendAsync(request, cancellationToken);

            //try {
            IPrincipal principal = _tokenManager.Validate(token);
            if (principal != null)
                request.GetRequestContext().Principal = principal;
            
            //} catch (SecurityTokenExpiredException e) {
            //    _logger.ErrorFormat("Security token expired: {0}", e);

            //    var response = new HttpResponseMessage((HttpStatusCode)440) {
            //        Content = new StringContent("Security token expired exception")
            //    };

            //    var tsc = new TaskCompletionSource<HttpResponseMessage>();
            //    tsc.SetResult(response);
            //    return tsc.Task;
            //} catch (SecurityTokenSignatureKeyNotFoundException e) {
            //    _logger.ErrorFormat("Error during JWT validation: {0}", e);

            //    var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) {
            //        Content = new StringContent("Untrusted signing cert")
            //    };

            //    var tsc = new TaskCompletionSource<HttpResponseMessage>();
            //    tsc.SetResult(response);
            //    return tsc.Task;
            //} catch (SecurityTokenValidationException e) {
            //    _logger.ErrorFormat("Error during JWT validation: {0}", e);
            //    throw;
            //} catch (Exception e) {
            //    _logger.ErrorFormat("Error during JWT validation: {0}", e);
            //    throw;
            //}

            return BaseSendAsync(request, cancellationToken);
        }
    }
}