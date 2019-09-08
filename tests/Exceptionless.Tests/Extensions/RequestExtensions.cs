using System;
using System.Net;
using System.Net.Http;
using Exceptionless.Tests.Utility;

namespace Exceptionless.Tests.Extensions {
    public static class RequestExtensions {
        public static AppSendBuilder StatusCodeShouldBeOk(this AppSendBuilder builder) {
            return builder.ExpectedStatus(HttpStatusCode.OK);
        }

        public static AppSendBuilder StatusCodeShouldBeAccepted(this AppSendBuilder builder) {
            return builder.ExpectedStatus(HttpStatusCode.Accepted);
        }

        public static AppSendBuilder StatusCodeShouldBeBadRequest(this AppSendBuilder builder) {
            return builder.ExpectedStatus(HttpStatusCode.BadRequest);
        }

        public static AppSendBuilder StatusCodeShouldBeCreated(this AppSendBuilder builder) {
            return builder.ExpectedStatus(HttpStatusCode.Created);
        }

        public static AppSendBuilder StatusCodeShouldBeUnauthorized(this AppSendBuilder builder) {
            return builder.ExpectedStatus(HttpStatusCode.Unauthorized);
        }

        private const string _expectedStatusKey = "ExpectedStatus";
        public static HttpStatusCode? GetExpectedStatus(this HttpRequestMessage requestMessage) {
            if (requestMessage == null)
                throw new ArgumentNullException(nameof(requestMessage));

            requestMessage.Properties.TryGetValue(_expectedStatusKey, out object propertyValue);
            return (HttpStatusCode?)propertyValue;
        }

        public static void SetExpectedStatus(this HttpRequestMessage requestMessage, HttpStatusCode statusCode) {
            if (requestMessage == null)
                throw new ArgumentNullException(nameof(requestMessage));

            requestMessage.Properties[_expectedStatusKey] = statusCode;
        }
    }
}