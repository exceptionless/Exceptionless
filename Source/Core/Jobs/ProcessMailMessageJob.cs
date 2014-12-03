using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues;
using NLog.Fluent;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Core.Jobs {
    public class ProcessMailMessageJob : JobBase {
        private readonly IQueue<MailMessage> _queue;
        private readonly IMailSender _mailSender;
        private readonly IAppStatsClient _statsClient;

        public ProcessMailMessageJob(IQueue<MailMessage> queue, IMailSender mailSender, IAppStatsClient statsClient) {
            _queue = queue;
            _mailSender = mailSender;
            _statsClient = statsClient;
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
            Log.Info().Message("Process email message job starting").Write();

            QueueEntry<MailMessage> queueEntry = null;
            try {
                queueEntry = _queue.Dequeue();
            } catch (Exception ex) {
                if (!(ex is TimeoutException)) {
                    Log.Error().Exception(ex).Message("An error occurred while trying to dequeue the next MailMessageNotification: {0}", ex.Message).Write();
                    return JobResult.FromException(ex);
                }
            }
            if (queueEntry == null)
                return JobResult.Success;
                
            _statsClient.Counter(StatNames.EmailsDequeued);
                
            Log.Info().Message("Processing MailMessageNotification '{0}'.", queueEntry.Id).Write();
                
            try {
                await _mailSender.SendAsync(queueEntry.Value);
                _statsClient.Counter(StatNames.EmailsSent);
            } catch (Exception ex) {
                _statsClient.Counter(StatNames.EmailsSendErrors);
                queueEntry.Abandon();

                Log.Error().Exception(ex).Message("Error sending message '{0}': {1}", queueEntry.Id, ex.Message).Write();
            }

            queueEntry.Complete();

            return JobResult.Success;
        }
    }
}