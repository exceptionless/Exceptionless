#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Api.Utility {
    public sealed class OverageHandler : DelegatingHandler {
        private readonly IOrganizationRepository _organizationRepository;

        public OverageHandler(IOrganizationRepository organizationRepository) {
            _organizationRepository = organizationRepository;
        }

        private bool IsEventPost(HttpRequestMessage request) {
            return request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath.Contains("/events");
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (!IsEventPost(request))
                return base.SendAsync(request, cancellationToken);

            string organizationId = request.GetDefaultOrganizationId();
            if (String.IsNullOrEmpty(organizationId))
                return CreateResponse(request, HttpStatusCode.Unauthorized, "Unauthorized");

            bool overLimit = _organizationRepository.IncrementUsage(organizationId);
            return overLimit ? CreateResponse(request, HttpStatusCode.PaymentRequired, "Event limit exceeded.") : base.SendAsync(request, cancellationToken);
        }

        private Task<HttpResponseMessage> CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);

            return Task.FromResult(response);
        }
    }
}