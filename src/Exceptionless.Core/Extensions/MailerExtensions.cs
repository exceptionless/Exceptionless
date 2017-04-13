using System;
using System.IO;
using Exceptionless.Core.Queues.Models;

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

            foreach (var view in message.AlternateViews) {
                if (view.ContentType.MediaType == "text/html")
                    using (var reader = new StreamReader(view.ContentStream))
                        notification.HtmlBody = reader.ReadToEnd();

                if (view.ContentType.MediaType == "text/plain")
                    using (var reader = new StreamReader(view.ContentStream))
                        notification.TextBody = reader.ReadToEnd();

            }

            return notification;
        }
    }
}