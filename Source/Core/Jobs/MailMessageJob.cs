using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues;
using NLog.Fluent;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Core.Jobs {
    public class MailMessageJob : JobBase {
        private readonly IQueue<MailMessage> _queue;
        private readonly IMailSender _mailSender;
        private readonly IAppStatsClient _statsClient;

        public MailMessageJob(IQueue<MailMessage> queue, IMailSender mailSender, IAppStatsClient statsClient) {
            _queue = queue;
            _mailSender = mailSender;
            _statsClient = statsClient;
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
            Log.Trace().Message("Process mail message job starting").Write();

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
            
            _statsClient.Counter(StatNames.EmailsDequeued);
            
            Log.Info().Message("Processing message '{0}'.", queueEntry.Id).Write();
                
            try {
                await _mailSender.SendAsync(queueEntry.Value);
                Log.Info().Message("Sent message: to={0} subject=\"{1}\"", queueEntry.Value.To, queueEntry.Value.Subject).Write();
                _statsClient.Counter(StatNames.EmailsSent);
            } catch (Exception ex) {
                _statsClient.Counter(StatNames.EmailsSendErrors);
                queueEntry.Abandon();

                Log.Error().Exception(ex).Message("Error sending message: id={0} error={1}", queueEntry.Id, ex.Message).Write();
            }

            queueEntry.Complete();

            return JobResult.Success;
        }
    }
}