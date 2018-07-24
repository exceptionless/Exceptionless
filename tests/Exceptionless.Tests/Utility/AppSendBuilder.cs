using System;
using System.Net;
using System.Net.Http;
using Exceptionless.Core.Utility;
using FluentRest;

namespace Exceptionless.Tests.Utility {
    public class AppSendBuilder : PostBuilder<AppSendBuilder> {
        internal static readonly HttpMethod HttpPatch = new HttpMethod("PATCH");

        public AppSendBuilder(HttpRequestMessage request) : base(request) { }

        /// <summary>
        /// Sets HTTP request method.
        /// </summary>
        /// <param name="method">The header request method.</param>
        /// <returns>A fluent request builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="method" /> is <see langword="null" />.</exception>
        public AppSendBuilder Method(HttpMethod method) {
            RequestMessage.Method = method ?? throw new ArgumentNullException(nameof(method));
            return this;
        }

        /// <summary>
        /// Sets HTTP request method to POST.
        /// </summary>
        /// <returns>A fluent request builder.</returns>
        public AppSendBuilder Post() {
            return Method(HttpMethod.Post);
        }

        /// <summary>
        /// Sets HTTP request method to PUT.
        /// </summary>
        /// <returns>A fluent request builder.</returns>
        public AppSendBuilder Put() {
            return Method(HttpMethod.Put);
        }

        /// <summary>
        /// Sets HTTP request method to PATCH.
        /// </summary>
        /// <returns>A fluent request builder.</returns>
        public AppSendBuilder Patch() {
            return Method(HttpPatch);
        }

        /// <summary>
        /// Sets HTTP request method to DELETE.
        /// </summary>
        /// <returns>A fluent request builder.</returns>
        public AppSendBuilder Delete() {
            return Method(HttpMethod.Delete);
        }

        public AppSendBuilder AsGlobalAdminUser() {
            return this.BasicAuthorization(SampleDataService.TEST_USER_EMAIL, SampleDataService.TEST_USER_PASSWORD);
        }

        public AppSendBuilder AsClientUser() {
            return this.BearerToken(SampleDataService.TEST_API_KEY);
        }

        public bool IsAnonymous { get; private set; }
        public AppSendBuilder AsAnonymousUser() {
            IsAnonymous = true;
            return this;
        }
        
        public AppSendBuilder ExpectedStatus(HttpStatusCode statusCode) {
            RequestMessage.Properties["ExpectedStatus"] = statusCode;
            return this;
        }
    }
}