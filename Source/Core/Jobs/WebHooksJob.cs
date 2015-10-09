using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Newtonsoft.Json;

namespace Exceptionless.Core.Jobs {
    public class WebHooksJob : QueueProcessorJobBase<WebHookNotification> {
        private readonly IWebHookRepository _webHookRepository;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        
        public WebHooksJob(IQueue<WebHookNotification> queue, IWebHookRepository webHookRepository, JsonSerializerSettings settings) : base(queue) {
            _webHookRepository = webHookRepository;
            _jsonSerializerSettings = settings;
        }
        
        protected override async Task<JobResult> ProcessQueueEntryAsync(JobQueueEntryContext<WebHookNotification> context) {
            WebHookNotification body = context.QueueEntry.Value;
            bool shouldLog = body.ProjectId != Settings.Current.InternalProjectId;
            Logger.Trace().Project(body.ProjectId).Message("Process web hook call: id={0} project={1} url={2}", context.QueueEntry.Id, body.ProjectId, body.Url).WriteIf(shouldLog);

            var client = new HttpClient();
            try {
                var response = await client.PostAsJsonAsync(body.Url, body.Data.ToJson(Formatting.Indented, _jsonSerializerSettings), context.CancellationToken).AnyContext();
                if (response.StatusCode == HttpStatusCode.Gone) {
                    Logger.Warn().Project(body.ProjectId).Message("Deleting web hook: org={0} project={1} url={2}", body.OrganizationId, body.ProjectId, body.Url).Write();
                    await _webHookRepository.RemoveByUrlAsync(body.Url).AnyContext();
                }
                
                Logger.Info().Project(body.ProjectId).Message("Web hook POST complete: status={0} org={1} project={2} url={3}", response.StatusCode, body.OrganizationId, body.ProjectId, body.Url).WriteIf(shouldLog);
            } catch (Exception ex) {
                return JobResult.FromException(ex);
            }

            return JobResult.Success;
        }
    }
}