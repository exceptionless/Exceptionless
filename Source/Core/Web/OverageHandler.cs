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
using Exceptionless.Core.Pipeline;
using Exceptionless.Extensions;
using Exceptionless.Core.Extensions;
using ServiceStack.Redis;

namespace Exceptionless.Core.Web {
    public sealed class OverageHandler : DelegatingHandler {
        private readonly IRedisClientsManager _clientsManager;
        private readonly IOrganizationRepository _organizationRepository;

        public OverageHandler(IRedisClientsManager clientManager, IOrganizationRepository organizationRepository) {
            _clientsManager = clientManager;
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

        private string GetMonthlyUsageCacheKey(string organizationId) {
            return String.Concat("usage", ":month-", organizationId, ":", DateTime.UtcNow.Date.ToString("MM"));
        }

        private string GetUsageSavedCacheKey(string organizationId) {
            return String.Concat("usage-saved", ":", organizationId);
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

            using (var cacheClient = _clientsManager.GetCacheClient()) {
                string hourlyCacheKey = GetHourlyCounterCacheKey(organizationId);
                long hourlyErrorCount = cacheClient.Increment(hourlyCacheKey, 1, TimeSpan.FromMinutes(61));
                string monthlyCacheKey = GetMonthlyUsageCacheKey(organizationId);
                long monthlyErrorCount = cacheClient.Increment(monthlyCacheKey, 1, TimeSpan.FromDays(32), (uint)org.GetCurrentMonthlyUsage());
                bool justWentOverHourly = hourlyErrorCount == org.GetHourlyErrorLimit() + 1;
                bool justWentOverMonthly = monthlyErrorCount == org.MaxErrorsPerMonth + 1;
                bool overLimit = hourlyErrorCount > org.GetHourlyErrorLimit() || monthlyErrorCount > org.MaxErrorsPerMonth;

                if (justWentOverHourly)
                    using (IRedisClient client = _clientsManager.GetClient())
                        client.PublishMessage(NotifySignalRAction.NOTIFICATION_CHANNEL_KEY, String.Concat("overlimit:hr:", org.Id));

                if (justWentOverMonthly)
                    using (IRedisClient client = _clientsManager.GetClient())
                        client.PublishMessage(NotifySignalRAction.NOTIFICATION_CHANNEL_KEY, String.Concat("overlimit:month:", org.Id));

                var lastCounterSavedDate = cacheClient.Get<DateTime?>(GetUsageSavedCacheKey(organizationId));
                if (!justWentOverHourly && !justWentOverMonthly && lastCounterSavedDate.HasValue && DateTime.UtcNow.Subtract(lastCounterSavedDate.Value).TotalMinutes < 5)
                    return overLimit ? CreateResponse(request, HttpStatusCode.PaymentRequired, "Error limit exceeded.") : base.SendAsync(request, cancellationToken);

                org = _organizationRepository.GetById(organizationId, true);
                org.SetMonthlyUsage(monthlyErrorCount);
                if (hourlyErrorCount > org.GetHourlyErrorLimit())
                    org.SetHourlyOverage(hourlyErrorCount);

                _organizationRepository.Update(org);
                cacheClient.Set(GetUsageSavedCacheKey(organizationId), DateTime.UtcNow, TimeSpan.FromDays(32));

                return overLimit ? CreateResponse(request, HttpStatusCode.PaymentRequired, "Error limit exceeded.") : base.SendAsync(request, cancellationToken);
            }
        }

        private Task<HttpResponseMessage> CreateResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message) {
            HttpResponseMessage response = request.CreateResponse(statusCode);
            response.ReasonPhrase = message;
            response.Content = new StringContent(message);

            return Task.FromResult(response);
        }
    }
}