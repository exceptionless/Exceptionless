using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Sends queued email messages.", InitialDelay = "5s")]
    public class MailMessageJob : QueueJobBase<MailMessage> {
        private readonly IMailSender _mailSender;

        public MailMessageJob(IQueue<MailMessage> queue, IMailSender mailSender, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _mailSender = mailSender;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<MailMessage> context) {
            _logger.Trace("Processing message '{0}'.", context.QueueEntry.Id);

            try {
                await _mailSender.SendAsync(context.QueueEntry.Value).AnyContext();
                _logger.Info("Sent message: to={0} subject=\"{1}\"", context.QueueEntry.Value.To, context.QueueEntry.Value.Subject);
            } catch (Exception ex) {
                return JobResult.FromException(ex);
            }

            return JobResult.Success;
        }
    }
}
