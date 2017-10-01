using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Foundatio.Queues;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Processes queued web hook messages.", InitialDelay = "5s")]
    public class WebHooksJob : QueueJobBase<WebHookNotification>, IDisposable {
        private readonly HttpClient _client = new HttpClient();
        private readonly IProjectRepository _projectRepository;
        private readonly SlackService _slackService;
        private readonly IWebHookRepository _webHookRepository;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public WebHooksJob(IQueue<WebHookNotification> queue, IProjectRepository projectRepository, SlackService slackService, IWebHookRepository webHookRepository, JsonSerializerSettings settings, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _projectRepository = projectRepository;
            _slackService = slackService;
            _webHookRepository = webHookRepository;
            _jsonSerializerSettings = settings;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<WebHookNotification> context) {
            var body = context.QueueEntry.Value;
            bool shouldLog = body.ProjectId != Settings.Current.InternalProjectId;
            using (_logger.BeginScope(new ExceptionlessState().Organization(body.OrganizationId).Project(body.ProjectId))) {
                if (shouldLog) _logger.LogTrace("Process web hook call: id={Id} project={1} url={Url}", context.QueueEntry.Id, body.ProjectId, body.Url);

                HttpResponseMessage response = null;
                try {
                    response = await _client.PostAsJsonAsync(body.Url, body.Data.ToJson(Formatting.Indented, _jsonSerializerSettings), context.CancellationToken).AnyContext();
                } catch (Exception ex) {
                    if (shouldLog) _logger.LogError(ex, "Error calling web hook: status={Status} org={organization} project={project} url={Url}", response?.StatusCode, body.OrganizationId, body.ProjectId, body.Url);
                    return JobResult.FromException(ex);
                }

                if ((int)response.StatusCode == 429 && response.Headers.RetryAfter.Date.HasValue) {
                    // TODO: Better handle rate limits
                    // throw new RateLimitException { RetryAfter = response.Headers.RetryAfter.Date.Value.UtcDateTime };

                    if (shouldLog) _logger.LogWarning("Web hook rate limit reached: status={Status} org={organization} project={project} url={Url}", response.StatusCode, body.OrganizationId, body.ProjectId, body.Url);
                    return JobResult.FailedWithMessage("Rate limit exceeded");
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
                   if (shouldLog)  _logger.LogWarning("Deleting web hook: status={Status} org={organization} project={project} url={Url}", response.StatusCode, body.OrganizationId, body.ProjectId, body.Url);
                    await RemoveIntegrationAsync(body).AnyContext();
                }

                if (shouldLog) _logger.LogInformation("Web hook POST complete: status={Status} org={organization} project={project} url={Url}", response.StatusCode, body.OrganizationId, body.ProjectId, body.Url);
            }

            return JobResult.Success;
        }

        private async Task RemoveIntegrationAsync(WebHookNotification body) {
            switch (body.Type) {
                case WebHookType.General:
                    await _webHookRepository.RemoveAsync(body.WebHookId).AnyContext();
                    break;
                case WebHookType.Slack:
                    var project = await _projectRepository.GetByIdAsync(body.ProjectId).AnyContext();
                    var token = project?.GetSlackToken();
                    if (token == null)
                        return;

                    await _slackService.RevokeAccessTokenAsync(token.AccessToken).AnyContext();
                    if (project.NotificationSettings.Remove(Project.NotificationIntegrations.Slack) | project.Data.Remove(Project.KnownDataKeys.SlackToken))
                        await _projectRepository.SaveAsync(project, o => o.Cache());

                    break;
            }
        }

        public void Dispose() {
            _client?.Dispose();
        }
    }
}