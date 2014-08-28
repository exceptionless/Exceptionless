using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues;
using NLog.Fluent;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Core.Jobs {
    public class ProcessMailMessageJob : Job {
        private readonly IQueue<MailMessage> _queue;
        private readonly IMailSender _mailSender;
        private readonly IAppStatsClient _statsClient;

        public ProcessMailMessageJob(IQueue<MailMessage> queue, IMailSender mailSender, IAppStatsClient statsClient) {
            _queue = queue;
            _mailSender = mailSender;
            _statsClient = statsClient;
        }

        public void Run(int totalEmailsToProcess) {
            Run(new JobRunContext().WithWorkItemLimit(totalEmailsToProcess));
        }

        protected async override Task<JobResult> RunInternalAsync() {
            Log.Info().Message("Process email message job starting").Write();
            int totalEmailsProcessed = 0;
            int totalEmailsToProcess = Context.GetWorkItemLimit();

            while (!CancelPending && (totalEmailsToProcess == -1 || totalEmailsProcessed < totalEmailsToProcess)) {
                QueueEntry<MailMessage> queueEntry = null;
                try {
                    queueEntry = await _queue.DequeueAsync();
                } catch (Exception ex) {
                    if (!(ex is TimeoutException)) {
                        Log.Error().Exception(ex).Message("An error occurred while trying to dequeue the next MailMessageNotification: {0}", ex.Message).Write();
                        return JobResult.FromException(ex);
                    }
                }
                if (queueEntry == null)
                    continue;
                
                _statsClient.Counter(StatNames.EmailsDequeued);

                Log.Info().Message("Processing MailMessageNotification '{0}'.", queueEntry.Id).Write();
                
                try {
                    await _mailSender.SendAsync(queueEntry.Value);
                    totalEmailsProcessed++;
                    _statsClient.Counter(StatNames.EmailsSent);
                } catch (Exception ex) {
                    _statsClient.Counter(StatNames.EmailsSendErrors);
                    queueEntry.AbandonAsync().Wait();

                    Log.Error().Exception(ex).Message("Error sending message '{0}': {1}", queueEntry.Id, ex.Message).Write();
                }

                await queueEntry.CompleteAsync();
            }

            return JobResult.Success;
        }
    }
}