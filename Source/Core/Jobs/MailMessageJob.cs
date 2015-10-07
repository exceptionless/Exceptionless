using System;
using System.Threading.Tasks;
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

        protected override async Task<JobResult> ProcessQueueEntryAsync(JobQueueEntryContext<MailMessage> context) {
            Log.Trace().Message("Processing message '{0}'.", context.QueueEntry.Id).Write();
            
            try {
                await _mailSender.SendAsync(context.QueueEntry.Value).AnyContext();
                Log.Info().Message("Sent message: to={0} subject=\"{1}\"", context.QueueEntry.Value.To, context.QueueEntry.Value.Subject).Write();
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error sending message: id={0} error={1}", context.QueueEntry.Id, ex.Message).Write();
                return JobResult.FromException(ex);
            }

            return JobResult.Success;
        }
    }
}