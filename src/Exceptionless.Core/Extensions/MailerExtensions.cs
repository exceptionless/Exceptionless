using System;
using System.IO;
using System.Net.Mail;
using MimeKit;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Core.Extensions {
    public static class MailerExtensions {
        public static MailMessage ToMailMessage(this System.Net.Mail.MailMessage message) {
            var notification = new MailMessage {
                To = message.To.ToString(),
                From = message.From?.ToString(),
                Subject = message.Subject
            };

            if (message.AlternateViews.Count == 0)
                throw new ArgumentException("MailMessage must contain an alternative view.", nameof(message));

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

        public static MimeMessage ToMailMessage(this MailMessage notification) {

            var message = new MimeMessage();
            var builder = new BodyBuilder();

            if (!String.IsNullOrEmpty(notification.From))
                message.From.Add(new MailboxAddress(notification.From));

            if (!String.IsNullOrEmpty(notification.To))
                message.To.Add(new MailboxAddress(notification.To));

            if (!String.IsNullOrEmpty(notification.TextBody))
                builder.TextBody = notification.TextBody;

            if (!String.IsNullOrEmpty(notification.HtmlBody))
                builder.HtmlBody = notification.HtmlBody;

            message.Body = builder.ToMessageBody();
            return message;
        }

        public static System.Net.Mail.MailMessage ToSystemNetMailMessage(this MailMessage notification)
        {
            var message = new System.Net.Mail.MailMessage { Subject = notification.Subject };
            if (!String.IsNullOrEmpty(notification.To))
                message.To.Add(notification.To);

            if (!String.IsNullOrEmpty(notification.From))
                message.From = new MailAddress(notification.From);

            if (!String.IsNullOrEmpty(notification.TextBody))
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(notification.TextBody, null, "text/plain"));

            if (!String.IsNullOrEmpty(notification.HtmlBody))
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(notification.HtmlBody, null, "text/html"));

            return message;
        }
    }
}
