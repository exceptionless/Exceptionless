using Exceptionless.Core.Queues.Models;

namespace Exceptionless.Core.Mail;

public interface IMailSender {
    Task SendAsync(MailMessage model);
}
