#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Models;
using NLog.Fluent;
using RazorSharpEmail;

namespace Exceptionless.Core.Mail {
    public class Mailer : IMailer {
        private readonly IEmailGenerator _emailGenerator;

        public Mailer(IEmailGenerator emailGenerator) {
            _emailGenerator = emailGenerator;
        }

        public void SendPasswordReset(User user) {
            if (user == null || String.IsNullOrEmpty(user.PasswordResetToken))
                return;

            MailMessage msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "PasswordReset");
            msg.To.Add(user.EmailAddress);
            SendMessage(msg);
        }

        public Task SendPasswordResetAsync(User sender) {
            return Task.Run(() => SendPasswordReset(sender));
        }

        public void SendVerifyEmail(User user) {
            MailMessage msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "VerifyEmail");
            msg.To.Add(user.EmailAddress);
            SendMessage(msg);
        }

        public Task SendVerifyEmailAsync(User user) {
            return Task.Run(() => SendVerifyEmail(user));
        }

        public void SendInvite(User sender, Organization organization, Invite invite) {
            MailMessage msg = _emailGenerator.GenerateMessage(new InviteModel {
                Sender = sender,
                Organization = organization,
                Invite = invite,
                BaseUrl = Settings.Current.BaseURL
            }, "Invite");
            msg.To.Add(invite.EmailAddress);
            SendMessage(msg);
        }

        public Task SendInviteAsync(User sender, Organization organization, Invite invite) {
            return Task.Run(() => SendInvite(sender, organization, invite));
        }

        public void SendPaymentFailed(User owner, Organization organization) {
            MailMessage msg = _emailGenerator.GenerateMessage(new PaymentModel {
                Owner = owner,
                Organization = organization,
                BaseUrl = Settings.Current.BaseURL
            }, "PaymentFailed");
            msg.To.Add(owner.EmailAddress);
            SendMessage(msg);
        }

        public Task SendPaymentFailedAsync(User owner, Organization organization) {
            return Task.Run(() => SendPaymentFailed(owner, organization));
        }

        public void SendAddedToOrganization(User sender, Organization organization, User user) {
            MailMessage msg = _emailGenerator.GenerateMessage(new AddedToOrganizationModel {
                Sender = sender,
                Organization = organization,
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "AddedToOrganization");
            msg.To.Add(user.EmailAddress);
            SendMessage(msg);
        }

        public Task SendAddedToOrganizationAsync(User sender, Organization organization, User user) {
            return Task.Run(() => SendAddedToOrganization(sender, organization, user));
        }

        public void SendNotice(string emailAddress, EventNotificationModel notification) {
            string notificationType = String.Concat(notification.TypeName, " Occurrence");
            if (notification.IsNew)
                notificationType = String.Concat("New ", notification.TypeName);
            else if (notification.IsRegression)
                notificationType = String.Concat(notification.TypeName, " Regression");

            if (notification.IsCritical)
                notificationType = String.Concat("Critical ", notificationType);

            notification.BaseUrl = Settings.Current.BaseURL;
            notification.NotificationType = notificationType;

            MailMessage msg = _emailGenerator.GenerateMessage(notification, "Notice");
            msg.To.Add(emailAddress);
            msg.Headers.Add("X-Mailer-Machine", Environment.MachineName);
            msg.Headers.Add("X-Mailer-Date", DateTime.Now.ToString());
            SendMessage(msg);
        }

        public Task SendNoticeAsync(string emailAddress, EventNotificationModel notification) {
            return Task.Run(() => SendNotice(emailAddress, notification));
        }

        public void SendSummaryNotification(string emailAddress, SummaryNotificationModel notification) {
            notification.BaseUrl = Settings.Current.BaseURL;
            MailMessage msg = _emailGenerator.GenerateMessage(notification, "SummaryNotification");
            msg.To.Add(emailAddress);
            msg.Headers.Add("X-Mailer-Machine", Environment.MachineName);
            msg.Headers.Add("X-Mailer-Date", DateTime.Now.ToString());
            SendMessage(msg);
        }

        public Task SendSummaryNotificationAsync(string emailAddress, SummaryNotificationModel notification) {
            return Task.Run(() => SendSummaryNotification(emailAddress, notification));
        }

        private void SendMessage(MailMessage message) {
            var client = new SmtpClient();
            CleanAddresses(message);

            try {
                client.Send(message);
            } catch (SmtpException ex) {
                var wex = ex.InnerException as WebException;
                if (ex.StatusCode == SmtpStatusCode.GeneralFailure && wex != null && wex.Status == WebExceptionStatus.ConnectFailure)
                    Log.Error().Exception(ex).Message(String.Format("Unable to connect to the mail server. Exception: {0}", wex.Message)).Write();
                else
                    throw;
            }
        }

        private static void CleanAddresses(MailMessage msg) {
            if (Settings.Current.WebsiteMode == WebsiteMode.Production)
                return;

            var invalid = new List<string>();
            invalid.AddRange(CleanAddresses(msg.To));
            invalid.AddRange(CleanAddresses(msg.CC));
            invalid.AddRange(CleanAddresses(msg.Bcc));

            if (invalid.Count == 0)
                return;

            if (invalid.Count <= 3)
                msg.Subject = String.Concat("[", invalid.ToDelimitedString(), "] ", msg.Subject).StripInvisible();

            msg.To.Add(Settings.Current.TestEmailAddress);
        }

        private static IEnumerable<string> CleanAddresses(MailAddressCollection mac) {
            var invalid = new List<string>();
            for (int i = 0; i < mac.Count; i++) {
                if (!Settings.Current.AllowedOutboundAddresses.Contains(v => mac[i].Address.ToLowerInvariant().Contains(v))) {
                    invalid.Add(mac[i].Address);
                    mac.RemoveAt(i);
                    i--;
                }
            }

            return invalid;
        }
    }
}