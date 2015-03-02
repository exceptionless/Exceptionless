using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using System.Web.Http.Results;
using Exceptionless.Core.Utility;

#pragma warning disable 1998

namespace Exceptionless.Api.Utility {
    public class ExceptionlessReferenceIdExceptionHandler : IExceptionHandler {
        private readonly ICoreLastReferenceIdManager _coreLastReferenceIdManager;

        public ExceptionlessReferenceIdExceptionHandler(ICoreLastReferenceIdManager coreLastReferenceIdManager) {
            _coreLastReferenceIdManager = coreLastReferenceIdManager;
        }

        public async Task HandleAsync(ExceptionHandlerContext context, CancellationToken cancellationToken) {
            if (context == null)
                throw new ArgumentNullException("context");

            var exceptionContext = context.ExceptionContext;
            var request = exceptionContext.Request;
            if (request == null)
                throw new ArgumentException(String.Format("{0}.{1} must not be null", typeof(ExceptionContext).Name, "Request"), "context");

            context.Result = new ResponseMessageResult(CreateErrorResponse(request, exceptionContext.Exception, HttpStatusCode.InternalServerError));
         }

        private HttpResponseMessage CreateErrorResponse(HttpRequestMessage request, Exception ex, HttpStatusCode statusCode) {
            HttpConfiguration configuration = request.GetConfiguration();
            HttpError error = new HttpError(ex, request.ShouldIncludeErrorDetail());

            string lastId = _coreLastReferenceIdManager.GetLastReferenceId();
            if (!String.IsNullOrEmpty(lastId))
                error.Add("Reference", lastId);

            // CreateErrorResponse should never fail, even if there is no configuration associated with the request
            // In that case, use the default HttpConfiguration to con-neg the response media type
            if (configuration == null) {
                using (HttpConfiguration defaultConfig = new HttpConfiguration()) {
                    return request.CreateResponse(statusCode, error, defaultConfig);
                }
            }

            return request.CreateResponse(statusCode, error, configuration);
        }
    }
}