using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Api.Serialization;

namespace Exceptionless.Api.Utility.Results {
    public class StatusCodeActionResult : IHttpActionResult {
        public StatusCodeActionResult(HttpStatusCode statusCode, HttpRequestMessage request, string message = null) {
            if (request == null)
                throw new ArgumentNullException("request");

            StatusCode = statusCode;
            Message = message ?? String.Empty;
            Request = request;
        }

        public HttpStatusCode StatusCode { get; private set; }
        public string Message { get; private set; }
        public HttpRequestMessage Request { get; private set; }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken) {
            return Task.FromResult(Execute());
        }

        private HttpResponseMessage Execute() {
            return new HttpResponseMessage(StatusCode) {
                ReasonPhrase = Message,
                Content = new ObjectContent<MessageContent>(new MessageContent(Message), new ExceptionlessJsonMediaTypeFormatter()),
                RequestMessage = Request
            };
        }
    }
}