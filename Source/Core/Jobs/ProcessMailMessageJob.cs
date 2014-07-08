using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues;
using NLog.Fluent;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Core.Jobs {
    public class ProcessMailMessageJob : Job {
        private readonly IQueue<MailMessage> _queue;
        private readonly IAppStatsClient _statsClient;

        public ProcessMailMessageJob(IQueue<MailMessage> queue, IAppStatsClient statsClient) {
            _queue = queue;
            _statsClient = statsClient;
        }

        public void Run(int totalEmailsToProcess) {
            var context = new JobRunContext();
            context.Properties.Add("TotalEmailsToProcess", totalEmailsToProcess);
            Run(context);
        }

        public async override Task<JobResult> RunAsync(JobRunContext context) {
            Log.Info().Message("Process email message job starting").Write();
            int totalEmailsProcessed = 0;
            int totalEmailsToProcess = -1;
            if (context.Properties.ContainsKey("TotalEmailsToProcess"))
                totalEmailsToProcess = (int)context.Properties["TotalEmailsToProcess"];

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
                    await SendMessage(queueEntry.Value);
                    totalEmailsProcessed++;
                    _statsClient.Counter(StatNames.EmailsSent);
                } catch (Exception ex) {
                    _statsClient.Counter(StatNames.EmailsSendErrors);
                    queueEntry.AbandonAsync().Wait();

                    // TODO: Add the MailMessageNotification to the logged exception.
                    Log.Error().Exception(ex).Message("An error occurred while processing the MailMessageNotification '{0}': {1}", queueEntry.Id, ex.Message).Write();
                    continue;
                }

                await queueEntry.CompleteAsync();
            }

            return JobResult.Success;
        }

        private async Task SendMessage(MailMessage notification) {
            var client = new SmtpClient();
            try {
                var message = notification.ToMailMessage();
                message.Headers.Add("X-Mailer-Machine", Environment.MachineName);
                message.Headers.Add("X-Mailer-Date", DateTime.Now.ToString());

                await client.SendAsync(message);
            } catch (SmtpException ex) {
                var wex = ex.InnerException as WebException;
                if (ex.StatusCode == SmtpStatusCode.GeneralFailure && wex != null && wex.Status == WebExceptionStatus.ConnectFailure)
                    Log.Error().Exception(ex).Message(String.Format("Unable to connect to the mail server. Exception: {0}", wex.Message)).Write();
                else
                    throw;
            }
        }
    }
}