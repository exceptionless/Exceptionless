using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Queues;
using Newtonsoft.Json;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class WebHooksJob : QueueProcessorJobBase<WebHookNotification> {
        private readonly IWebHookRepository _webHookRepository;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        
        public WebHooksJob(IQueue<WebHookNotification> queue, IWebHookRepository webHookRepository, JsonSerializerSettings settings) : base(queue) {
            _webHookRepository = webHookRepository;
            _jsonSerializerSettings = settings;

            AutoComplete = true;
        }
        
        protected override async Task<JobResult> ProcessQueueItemAsync(QueueEntry<WebHookNotification> queueEntry) {
            WebHookNotification body = queueEntry.Value;
            bool shouldLog = body.ProjectId != Settings.Current.InternalProjectId;
            Log.Trace().Project(body.ProjectId).Message("Process web hook call: id={0} project={1} url={2}", queueEntry.Id, body.ProjectId, body.Url).WriteIf(shouldLog);

            var client = new HttpClient();
            try {
                var response = await client.PostAsJsonAsync(body.Url, body.Data.ToJson(Formatting.Indented, _jsonSerializerSettings)).AnyContext();
                if (response.StatusCode == HttpStatusCode.Gone) {
                    Log.Warn().Project(body.ProjectId).Message("Deleting web hook: org={0} project={1} url={2}", body.OrganizationId, body.ProjectId, body.Url).Write();
                    _webHookRepository.RemoveByUrl(body.Url);
                }
                
                Log.Info().Project(body.ProjectId).Message("Web hook POST complete: status={0} org={1} project={2} url={3}", response.StatusCode, body.OrganizationId, body.ProjectId, body.Url).WriteIf(shouldLog);
            } catch (Exception ex) {
                return JobResult.FromException(ex);
            }

            return JobResult.Success;
        }
    }
}