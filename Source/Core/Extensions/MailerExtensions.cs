using System;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Core.Extensions {
    public static class MailerExtensions {
        public static MailMessage ToMailMessage(this System.Net.Mail.MailMessage message) {
            var notification = new MailMessage {
                To = message.To.ToString(),
                From = message.From != null ? message.From.ToString() : null,
                Subject = message.Subject
            };

            if (message.AlternateViews.Count == 0)
                throw new ArgumentException("MailMessage must contain an alternative view.", "message");

            foreach (AlternateView view in message.AlternateViews) {
                if (view.ContentType.MediaType == "text/html")
                    using (var reader = new StreamReader(view.ContentStream))
                        notification.HtmlBody = reader.ReadToEnd();

                if (view.ContentType.MediaType == "text/plain")
                    using (var reader = new StreamReader(view.ContentStream))
                        notification.TextBody = reader.ReadToEnd();

            }

            return notification;
        }

        public static System.Net.Mail.MailMessage ToMailMessage(this MailMessage notification) {
            var message = new System.Net.Mail.MailMessage(notification.From, notification.To) { Subject = notification.Subject };

            if (!String.IsNullOrEmpty(notification.TextBody))
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(notification.TextBody, null, "text/plain"));

            if (!String.IsNullOrEmpty(notification.HtmlBody))
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(notification.HtmlBody, null, "text/html"));

            return message;
        }

        public static async Task SendAsync(this SmtpClient client, System.Net.Mail.MailMessage message) {
            // TODO: Verify that this works and see if there is a better method.
            client.Send(message);
            await Task.Yield();
        }
    }
}
