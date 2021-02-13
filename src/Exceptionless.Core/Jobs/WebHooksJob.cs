﻿using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Foundatio.Queues;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

#nullable enable

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Processes queued web hook messages.", InitialDelay = "5s")]
    public class WebHooksJob : QueueJobBase<WebHookNotification>, IDisposable {
        private const string ConsecutiveErrorsCacheKey = "errors";
        private const string FirstAttemptCacheKey = "first-attempt";
        private const string LastAttemptCacheKey = "last-attempt";
        private static readonly string[] _cacheKeys = { ConsecutiveErrorsCacheKey, FirstAttemptCacheKey, LastAttemptCacheKey };

        private readonly IProjectRepository _projectRepository;
        private readonly SlackService _slackService;
        private readonly IWebHookRepository _webHookRepository;
        private readonly ICacheClient _cacheClient;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly AppOptions _appOptions;

        private HttpClient? _client;

        private HttpClient Client {
            get => _client ??= new HttpClient();
        }

        public WebHooksJob(IQueue<WebHookNotification> queue, IProjectRepository projectRepository, SlackService slackService, IWebHookRepository webHookRepository, ICacheClient cacheClient, JsonSerializerSettings settings, AppOptions appOptions, ILoggerFactory? loggerFactory = null) : base(queue, loggerFactory) {
            _projectRepository = projectRepository;
            _slackService = slackService;
            _webHookRepository = webHookRepository;
            _cacheClient = cacheClient;
            _jsonSerializerSettings = settings;
            _appOptions = appOptions;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<WebHookNotification> context) {
            var body = context.QueueEntry.Value;
            bool shouldLog = body.ProjectId != _appOptions.InternalProjectId;
            using (_logger.BeginScope(new ExceptionlessState().Organization(body.OrganizationId).Project(body.ProjectId))) {
                if (shouldLog) _logger.RecordWebHook(context.QueueEntry.Id, body.ProjectId, body.Url);

                if (!await IsEnabledAsync(body).AnyContext()) {
                    _logger.WebHookCancelled();
                    return JobResult.Cancelled;
                }
                
                var cache = new ScopedCacheClient(_cacheClient, GetCacheKeyScope(body));
                long consecutiveErrors = await cache.GetAsync<long>(ConsecutiveErrorsCacheKey, 0).AnyContext();
                if (consecutiveErrors > 10) {
                    var lastAttempt = await cache.GetAsync(LastAttemptCacheKey, SystemClock.UtcNow).AnyContext();
                    var nextAttemptAllowedAt = lastAttempt.AddMinutes(15);
                    if (nextAttemptAllowedAt >= SystemClock.UtcNow) {
                        _logger.WebHookCancelledBackoff(consecutiveErrors, nextAttemptAllowedAt);
                        return JobResult.Cancelled;
                    }
                }
                
                bool successful = true;
                HttpResponseMessage? response = null;
                try {
                    using (var timeoutCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5))) {
                        using (var postCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCancellationTokenSource.Token)) {
                            response = await Client.PostAsJsonAsync(body.Url, body.Data.ToJson(Formatting.Indented, _jsonSerializerSettings), postCancellationTokenSource.Token).AnyContext();
                            if (!response.IsSuccessStatusCode)
                                successful = false;
                            else if (consecutiveErrors > 0)
                                await cache.RemoveAllAsync(_cacheKeys).AnyContext();
                        }
                    }
                } catch (OperationCanceledException ex) {
                    successful = false;
                    if (shouldLog) _logger.WebHookTimeout(response?.StatusCode, body.OrganizationId, body.ProjectId, body.Url, ex);
                    return JobResult.Cancelled;
                } catch (Exception ex) {
                    successful = false;
                    if (shouldLog) _logger.WebHookError(response?.StatusCode, body.OrganizationId, body.ProjectId, body.Url, ex);
                    return JobResult.FromException(ex);
                } finally {
                    if (successful) {
                        _logger.WebHookComplete(response?.StatusCode, body.OrganizationId, body.ProjectId, body.Url);
                    } else if (response != null && (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Gone)) {
                        _logger.WebHookDisabledStatusCode(body.Type == WebHookType.Slack ? "Slack" : body.WebHookId, response.StatusCode, body.OrganizationId, body.ProjectId, body.Url);
                        await DisableIntegrationAsync(body).AnyContext();
                        await cache.RemoveAllAsync(_cacheKeys).AnyContext();
                    } else {
                        var now = SystemClock.UtcNow;
                        await cache.SetAsync(LastAttemptCacheKey, now, TimeSpan.FromDays(3)).AnyContext();
                        consecutiveErrors = await cache.IncrementAsync(ConsecutiveErrorsCacheKey, TimeSpan.FromDays(3)).AnyContext();
                        DateTime firstAttempt;
                        if (consecutiveErrors == 1) {
                            await cache.SetAsync(FirstAttemptCacheKey, now, TimeSpan.FromDays(3)).AnyContext();
                            firstAttempt = now;
                        } else {
                            firstAttempt = await cache.GetAsync(FirstAttemptCacheKey, now).AnyContext();
                        }

                        if (consecutiveErrors >= 10) {
                            // don't retry any more
                            context.QueueEntry.MarkCompleted();
                            
                            // disable if more than 10 consecutive errors over the course of multiple days
                            if (firstAttempt.IsBefore(now.SubtractDays(2))) {
                                _logger.WebHookDisabledTooManyErrors(body.Type == WebHookType.Slack ? "Slack" : body.WebHookId);
                                await DisableIntegrationAsync(body).AnyContext();
                                await cache.RemoveAllAsync(_cacheKeys).AnyContext();
                            }
                        }
                    }
                }
            }

            return JobResult.Success;
        }

        private async Task<bool> IsEnabledAsync(WebHookNotification body) {
            switch (body.Type) {
                case WebHookType.General:
                    var webHook = await _webHookRepository.GetByIdAsync(body.WebHookId, o => o.Cache()).AnyContext();
                    return webHook?.IsEnabled ?? false;
                case WebHookType.Slack:
                    var project = await _projectRepository.GetByIdAsync(body.ProjectId, o => o.Cache()).AnyContext();
                    var token = project?.GetSlackToken();
                    return token != null;
            }

            return false;
        }
        
        private async Task DisableIntegrationAsync(WebHookNotification body) {
            switch (body.Type) {
                case WebHookType.General:
                    await _webHookRepository.MarkDisabledAsync(body.WebHookId).AnyContext();
                    break;
                case WebHookType.Slack:
                    var project = await _projectRepository.GetByIdAsync(body.ProjectId).AnyContext();
                    var token = project?.GetSlackToken();
                    if (token == null)
                        return;

                    Debug.Assert(project != null);

                    await _slackService.RevokeAccessTokenAsync(token.AccessToken).AnyContext();
                    if (project.NotificationSettings.Remove(Project.NotificationIntegrations.Slack) | project.Data.Remove(Project.KnownDataKeys.SlackToken))
                        await _projectRepository.SaveAsync(project, o => o.Cache());

                    break;
            }
        }
        
        private static string GetCacheKeyScope(WebHookNotification body) {
            return String.Concat("Project:", body.ProjectId, ":webhook:", body.Type == WebHookType.Slack ? "slack" : body.WebHookId);
        }

        public void Dispose() {
            _client?.Dispose();
        }
    }
}