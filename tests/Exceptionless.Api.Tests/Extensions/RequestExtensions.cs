using System;
using System.Net;
using FluentRest;

namespace Exceptionless.Api.Tests.Extensions {
    public static class RequestExtensions {
        public static SendBuilder StatusCodeShouldBeOk(this SendBuilder scenario) {
            return scenario.ExpectedStatus(HttpStatusCode.OK);
        }

        public static SendBuilder StatusCodeShouldBeAccepted(this SendBuilder scenario) {
            return scenario.ExpectedStatus(HttpStatusCode.Accepted);
        }

        public static SendBuilder StatusCodeShouldBeBadRequest(this SendBuilder scenario) {
            return scenario.ExpectedStatus(HttpStatusCode.BadRequest);
        }

        public static SendBuilder StatusCodeShouldBeUnauthorized(this SendBuilder scenario) {
            return scenario.ExpectedStatus(HttpStatusCode.Unauthorized);
        }
    }
}