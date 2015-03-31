using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Foundatio.Jobs;
using Foundatio.Metrics;
using Foundatio.Queues;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class MailMessageJob : JobBase {
        private readonly IQueue<MailMessage> _queue;
        private readonly IMailSender _mailSender;
        private readonly IMetricsClient _metricsClient;

        public MailMessageJob(IQueue<MailMessage> queue, IMailSender mailSender, IMetricsClient metricsClient) {
            _queue = queue;
            _mailSender = mailSender;
            _metricsClient = metricsClient;
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
            QueueEntry<MailMessage> queueEntry = null;
            try {
                queueEntry = _queue.Dequeue();
            } catch (Exception ex) {
                if (!(ex is TimeoutException)) {
                    Log.Error().Exception(ex).Message("Error trying to dequeue message: {0}", ex.Message).Write();
                    return JobResult.FromException(ex);
                }
            }

            if (queueEntry == null)
                return JobResult.Success;
            
            await _metricsClient.CounterAsync(MetricNames.EmailsDequeued);
            Log.Trace().Message("Processing message '{0}'.", queueEntry.Id).Write();
            
            try {
                await _mailSender.SendAsync(queueEntry.Value);
                await _metricsClient.CounterAsync(MetricNames.EmailsSent);
                Log.Info().Message("Sent message: to={0} subject=\"{1}\"", queueEntry.Value.To, queueEntry.Value.Subject).Write();
            } catch (Exception ex) {
                // TODO: Change to async once vnext is released.
                _metricsClient.Counter(MetricNames.EmailsSendErrors);
                Log.Error().Exception(ex).Message("Error sending message: id={0} error={1}", queueEntry.Id, ex.Message).Write();

                queueEntry.Abandon();
            }

            queueEntry.Complete();

            return JobResult.Success;
        }
    }
}