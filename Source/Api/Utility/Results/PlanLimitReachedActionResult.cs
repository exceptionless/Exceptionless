using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Api.Serialization;

namespace Exceptionless.Api.Utility.Results {
    public class PlanLimitReachedActionResult : IHttpActionResult {
        public PlanLimitReachedActionResult(string message, HttpRequestMessage request) {
            if (message == null) {
                throw new ArgumentNullException("message");
            }

            if (request == null) {
                throw new ArgumentNullException("request");
            }

            Message = message;
            Request = request;
        }

        public string Message { get; private set; }

        public HttpRequestMessage Request { get; private set; }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken) {
            return Task.FromResult(Execute());
        }

        private HttpResponseMessage Execute() {
            return new HttpResponseMessage(HttpStatusCode.UpgradeRequired) {
                ReasonPhrase = Message,
                Content = new ObjectContent<MessageContent>(new MessageContent(Message), new ExceptionlessJsonMediaTypeFormatter()),
                RequestMessage = Request
            };
        }
    }
}