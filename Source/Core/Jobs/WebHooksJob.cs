using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Newtonsoft.Json;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class WebHooksJob : JobBase {
        private readonly IQueue<WebHookNotification> _queue;
        private readonly IWebHookRepository _webHookRepository;
        private readonly IAppStatsClient _statsClient;

        public WebHooksJob(IQueue<WebHookNotification> queue, IAppStatsClient statsClient, IWebHookRepository webHookRepository) {
            _queue = queue;
            _webHookRepository = webHookRepository;
            _statsClient = statsClient;
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
            Log.Trace().Message("Web hook job starting").Write();

            QueueEntry<WebHookNotification> queueEntry = null;
            try {
                queueEntry = _queue.Dequeue();
            } catch (Exception ex) {
                if (!(ex is TimeoutException)) {
                    Log.Error().Exception(ex).Message("An error occurred while trying to dequeue the next WebHookNotification: {0}", ex.Message).Write();
                    return JobResult.FromException(ex);
                }
            }
            if (queueEntry == null)
                return JobResult.Success;

            Log.Trace().Message("Processing WebHookNotification '{0}'.", queueEntry.Id).Write();

            WebHookNotification body = queueEntry.Value;
            Log.Trace().Project(body.ProjectId).Message("Process web hook call: project={0} url={1}", body.ProjectId, body.Url).Write();

            var client = new HttpClient();
            try {
                var result = client.PostAsJson(body.Url, body.Data.ToJson(Formatting.Indented));

                if (result.StatusCode == HttpStatusCode.Gone) {
                    _webHookRepository.RemoveByUrl(body.Url);
                    Log.Warn().Project(body.ProjectId).Message("Deleting web hook: org={0} project={1} url={2}", body.OrganizationId, body.ProjectId, body.Url).Write();
                }

                queueEntry.Complete();

                Log.Info().Project(body.ProjectId).Message("Web hook POST complete: status={0} org={1} project={2} url={3}", result.StatusCode, body.OrganizationId, body.ProjectId, body.Url).Write();
            } catch (Exception ex) {
                queueEntry.Abandon();
                return JobResult.FromException(ex);
            }

            return JobResult.Success;
        }
    }
}