#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.AppStats;
using NLog.Fluent;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core.Pipeline {
    [Priority(5)]
    public class ThrottleBotsAction : ErrorPipelineActionBase {
        private readonly ICacheClient _cacheClient;
        private readonly IAppStatsClient _stats;
        private readonly ErrorRepository _errorRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly TimeSpan _throttlingPeriod = TimeSpan.FromMinutes(5);

        public ThrottleBotsAction(ICacheClient cacheClient, ErrorRepository errorRepository, IProjectRepository projectRepository, IAppStatsClient stats) {
            _cacheClient = cacheClient;
            _errorRepository = errorRepository;
            _projectRepository = projectRepository;
            _stats = stats;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(ErrorPipelineContext ctx) {
            if (Settings.Current.WebsiteMode == WebsiteMode.Dev)
                return;

            var project = _projectRepository.GetByIdCached(ctx.Error.ProjectId);
            if (project == null || !project.DeleteBotDataEnabled)
                return;

            // Throttle errors by client ip address to no more than X every 5 minutes.
            string clientIp = null;
            if (ctx.Error.RequestInfo != null && !String.IsNullOrEmpty(ctx.Error.RequestInfo.ClientIpAddress))
                clientIp = ctx.Error.RequestInfo.ClientIpAddress;
            
            if (String.IsNullOrEmpty(clientIp))
                return;

            string throttleCacheKey = String.Concat("bot:", clientIp, ":", DateTime.Now.Floor(_throttlingPeriod).Ticks);
            var requestCount = _cacheClient.Get<int?>(throttleCacheKey);
            if (requestCount != null) {
                _cacheClient.Increment(throttleCacheKey, 1);
                requestCount++;
            } else {
                _cacheClient.Set(throttleCacheKey, 1, _throttlingPeriod);
                requestCount = 1;
            }

            if (requestCount < Settings.Current.BotThrottleLimit)
                return;

            _stats.Counter(StatNames.ErrorsBotThrottleTriggered);
            Log.Info().Message("Bot throttle triggered. IP: {0} Time: {1} Project: {2}", clientIp, DateTime.Now.Floor(_throttlingPeriod), ctx.Error.ProjectId).Project(ctx.Error.ProjectId).Write();
            // the throttle was triggered, go and delete all the errors that triggered the throttle to reduce bot noise in the system
            Task.Run(() => _errorRepository.RemoveAllByClientIpAndDateAsync(clientIp, DateTime.Now.Floor(_throttlingPeriod), DateTime.Now.Ceiling(_throttlingPeriod)));
            ctx.IsCancelled = true;
        }
    }
}