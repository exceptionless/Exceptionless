using Exceptionless.Core.Mail;
using Exceptionless.Core.Queues.Models;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Sends queued email messages.", InitialDelay = "5s")]
public class MailMessageJob : QueueJobBase<MailMessage>
{
    private readonly IMailSender _mailSender;

    public MailMessageJob(IQueue<MailMessage> queue, IMailSender mailSender, TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider, ILoggerFactory loggerFactory) : base(queue, timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _mailSender = mailSender;
    }

    protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<MailMessage> context)
    {
        _logger.LogTrace("Processing message {QueueEntryId}", context.QueueEntry.Id);

        try
        {
            await _mailSender.SendAsync(context.QueueEntry.Value);
            _logger.LogInformation("Sent message: to={To} subject={Subject}", context.QueueEntry.Value.To, context.QueueEntry.Value.Subject);
        }
        catch (Exception ex)
        {
            return JobResult.FromException(ex);
        }

        return JobResult.Success;
    }
}
