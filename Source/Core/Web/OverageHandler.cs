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
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Authorization;
using Exceptionless.Extensions;
using ServiceStack.CacheAccess;
using Exceptionless.Core.Extensions;

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
            var principal = ctx.Principal as ExceptionlessPrincipal;
            if (principal != null)
                return principal.Project != null ? principal.Project.OrganizationId : principal.UserEntity.OrganizationIds.FirstOrDefault();

            return null;
        }

        private bool IsErrorPost(HttpRequestMessage request) {
            return request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath.Contains("/error");
        }

        private string GetHourlyCounterCacheKey(string organizationId) {
            return String.Concat("overage", ":hr-", organizationId, ":", DateTime.UtcNow.Date.ToString("MMddHH"));
        }

        private string GetMonthlyCounterCacheKey(string organizationId) {
            return String.Concat("overage", ":month-", organizationId, ":", DateTime.UtcNow.Date.ToString("MM"));
        }

        private string GetCounterSavedCacheKey(string organizationId) {
            return String.Concat("overage-saved", ":", organizationId);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (!IsErrorPost(request))
                return base.SendAsync(request, cancellationToken);

            string organizationId = GetOrganizationId(request);
            if (String.IsNullOrEmpty(organizationId))
                return CreateResponse(request, HttpStatusCode.Unauthorized, "Unauthorized");

            var org = _organizationRepository.GetByIdCached(organizationId);
            if (org.MaxErrorsPerMonth < 0)
                return base.SendAsync(request, cancellationToken);

            // TODO: We need to be using the current value from the org for monthly error count if the
            // key doesn't exist in Redis since redis might restart. We only store the count if the plan is over
            // the limit, so we need to figure out a plan.
            string hourlyCacheKey = GetHourlyCounterCacheKey(organizationId);
            long hourlyErrorCount = _cacheClient.Increment(hourlyCacheKey, 1, TimeSpan.FromMinutes(61));
            string monthlyCacheKey = GetMonthlyCounterCacheKey(organizationId);
            long monthlyErrorCount = _cacheClient.Increment(monthlyCacheKey, 1, TimeSpan.FromDays(32));

            if (hourlyErrorCount <= org.GetHourlyErrorLimit() && monthlyErrorCount <= org.MaxErrorsPerMonth)
                return base.SendAsync(request, cancellationToken);

            var lastCounterSavedDate = _cacheClient.Get<DateTime?>(GetCounterSavedCacheKey(organizationId));
            if (lastCounterSavedDate.HasValue && DateTime.UtcNow.Subtract(lastCounterSavedDate.Value).TotalMinutes < 5)
                return CreateResponse(request, HttpStatusCode.PaymentRequired, "Error limit exceeded.");

            org = _organizationRepository.GetById(organizationId, true);
            if (hourlyErrorCount > org.GetHourlyErrorLimit())
                org.SetHourlyOverage(hourlyErrorCount);
            if (monthlyErrorCount > org.MaxErrorsPerMonth)
                org.SetMonthlyOverage(monthlyErrorCount);

            _organizationRepository.Update(org);
            _cacheClient.Set(GetCounterSavedCacheKey(organizationId), DateTime.UtcNow);
            return CreateResponse(request, HttpStatusCode.PaymentRequired, String.Format("Error limit exceeded."));
        }

        private Task<HttpResponseMessage> CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);

            return Task.FromResult(response);
        }
    }
}