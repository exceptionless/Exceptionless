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
using Foundatio.Logging;
using Foundatio.Repositories;
using Foundatio.Queues;
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
            _logger.Trace().Project(body.ProjectId).Message("Process web hook call: id={0} project={1} url={2}", context.QueueEntry.Id, body.ProjectId, body.Url).WriteIf(shouldLog);

            HttpResponseMessage response = null;
            try {
                response = await _client.PostAsJsonAsync(body.Url, body.Data.ToJson(Formatting.Indented, _jsonSerializerSettings), context.CancellationToken).AnyContext();
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Project(body.ProjectId).Message("Error calling web hook: status={0} org={1} project={2} url={3}", response?.StatusCode, body.OrganizationId, body.ProjectId, body.Url).WriteIf(shouldLog);
                return JobResult.FromException(ex);
            }

            if ((int)response.StatusCode == 429 && response.Headers.RetryAfter.Date.HasValue) {
                // TODO: Better handle rate limits
                // throw new RateLimitException { RetryAfter = response.Headers.RetryAfter.Date.Value.UtcDateTime };

                _logger.Warn().Project(body.ProjectId).Message("Web hook rate limit reached: status={0} org={1} project={2} url={3}", response.StatusCode, body.OrganizationId, body.ProjectId, body.Url).WriteIf(shouldLog);
                return JobResult.Success;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
                _logger.Warn().Project(body.ProjectId).Message("Deleting web hook: status={0} org={1} project={2} url={3}", response.StatusCode, body.OrganizationId, body.ProjectId, body.Url).WriteIf(shouldLog);
                await RemoveIntegrationAsync(body).AnyContext();
            }

            _logger.Info().Project(body.ProjectId).Message("Web hook POST complete: status={0} org={1} project={2} url={3}", response.StatusCode, body.OrganizationId, body.ProjectId, body.Url).WriteIf(shouldLog);
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