using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Foundatio.Jobs;
using Foundatio.Metrics;
using Foundatio.Queues;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class MailMessageJob : QueueProcessorJobBase<MailMessage> {
        private readonly IMailSender _mailSender;
        private readonly IMetricsClient _metricsClient;

        public MailMessageJob(IQueue<MailMessage> queue, IMailSender mailSender, IMetricsClient metricsClient) : base(queue) {
            _mailSender = mailSender;
            _metricsClient = metricsClient;
        }

        protected override async Task<JobResult> ProcessQueueItemAsync(QueueEntry<MailMessage> queueEntry, CancellationToken cancellationToken = default(CancellationToken)) {
            Log.Trace().Message("Processing message '{0}'.", queueEntry.Id).Write();
            
            try {
                await _mailSender.SendAsync(queueEntry.Value).AnyContext();
                Log.Info().Message("Sent message: to={0} subject=\"{1}\"", queueEntry.Value.To, queueEntry.Value.Subject).Write();
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error sending message: id={0} error={1}", queueEntry.Id, ex.Message).Write();
                return JobResult.FromException(ex);
            }

            return JobResult.Success;
        }
    }
}