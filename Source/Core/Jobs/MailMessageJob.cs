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
        private readonly IMetricsClient _statsClient;

        public MailMessageJob(IQueue<MailMessage> queue, IMailSender mailSender, IMetricsClient statsClient) {
            _queue = queue;
            _mailSender = mailSender;
            _statsClient = statsClient;
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
            
            _statsClient.Counter(MetricNames.EmailsDequeued);
            
            Log.Trace().Message("Processing message '{0}'.", queueEntry.Id).Write();
                
            try {
                await _mailSender.SendAsync(queueEntry.Value);
                Log.Info().Message("Sent message: to={0} subject=\"{1}\"", queueEntry.Value.To, queueEntry.Value.Subject).Write();
                _statsClient.Counter(MetricNames.EmailsSent);
            } catch (Exception ex) {
                _statsClient.Counter(MetricNames.EmailsSendErrors);
                queueEntry.Abandon();

                Log.Error().Exception(ex).Message("Error sending message: id={0} error={1}", queueEntry.Id, ex.Message).Write();
            }

            queueEntry.Complete();

            return JobResult.Success;
        }
    }
}