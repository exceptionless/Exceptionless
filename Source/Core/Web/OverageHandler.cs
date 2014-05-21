#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using Exceptionless.Core.Authorization;
using Exceptionless.Models;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core.Web {
    public sealed class OverageHandler : DelegatingHandler {
        private readonly ICacheClient _cacheClient;
        private readonly IOrganizationRepository _organizationRepository;

        public OverageHandler(ICacheClient cacheClient, IOrganizationRepository organizationRepository) {
            _cacheClient = cacheClient;
            _organizationRepository = organizationRepository;
        }

        private string GetOrganizationId(HttpRequestMessage request) {
            HttpRequestContext ctx = request.GetRequestContext();
            if (ctx == null)
                return null;

            // get the current principals associated organization
            var principal = request.GetRequestContext().Principal as ExceptionlessPrincipal;
            if (principal != null)
                return principal.Project != null ? principal.Project.OrganizationId : principal.UserEntity.OrganizationIds.FirstOrDefault();

            return null;
        }

        private bool IsErrorPost(HttpRequestMessage request) {
            return request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath.Contains("/error");
        }

        private string GetCounterCacheKey(string organizationId) {
            return String.Concat("overage", ":", organizationId, ":", DateTime.UtcNow.Date.ToString("MMdd"));
        }

        private string GetCounterSavedCacheKey(string organizationId) {
            return String.Concat("overage-saved", ":", organizationId, ":", DateTime.UtcNow.Date.ToString("MMdd"));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (!IsErrorPost(request))
                return base.SendAsync(request, cancellationToken);

            string organizationId = GetOrganizationId(request);
            if (String.IsNullOrEmpty(organizationId))
                return CreateResponse(request, HttpStatusCode.Forbidden, "Could not identify organization for error post.");

            var org = _organizationRepository.GetByIdCached(organizationId);

            string cacheKey = GetCounterCacheKey(organizationId);
            long errorCount = _cacheClient.Increment(cacheKey, 1);
            if (errorCount <= org.MaxErrorsPerDay)
                return base.SendAsync(request, cancellationToken);

            var lastCounterSavedDate = _cacheClient.Get<DateTime?>(GetCounterSavedCacheKey(organizationId));
            if (lastCounterSavedDate.HasValue && DateTime.UtcNow.Subtract(lastCounterSavedDate.Value).TotalMinutes < 5)
                return CreateResponse(request, HttpStatusCode.PaymentRequired, String.Format("Daily error limit ({0}) exceeded ({1}).", org.MaxErrorsPerDay, errorCount));

            org = _organizationRepository.GetById(organizationId, true);
            var overageInfo = org.OverageDays.FirstOrDefault(o => o.Day == DateTime.UtcNow.Date);
            if (overageInfo == null) {
                overageInfo = new OverageInfo {
                    Day = DateTime.UtcNow.Date,
                    Count = (int)errorCount,
                    Limit = org.MaxErrorsPerDay
                };
                org.OverageDays.Add(overageInfo);
            } else {
                overageInfo.Count = (int)errorCount;
            }

            _organizationRepository.Update(org);
            return CreateResponse(request, HttpStatusCode.PaymentRequired, String.Format("Daily error limit ({0}) exceeded ({1}).", org.MaxErrorsPerDay, errorCount));
        }

        private Task<HttpResponseMessage> CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);

            return Task.FromResult(response);
        }
    }
}