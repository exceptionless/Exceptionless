using System;
using System.Threading.Tasks;
using Exceptionless.Core.Queues.Models;

namespace Exceptionless.Core.Mail {
    public interface IMailSender {
        Task SendAsync(MailMessage model);
    }
}