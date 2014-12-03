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
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Models;
using RazorSharpEmail;
using MailMessage = System.Net.Mail.MailMessage;

namespace Exceptionless.Core.Mail {
    public class Mailer : IMailer {
        private readonly IEmailGenerator _emailGenerator;
        private readonly IQueue<Queues.Models.MailMessage> _queue;
        private readonly FormattingPluginManager _pluginManager;
        private readonly IAppStatsClient _statsClient;

        public Mailer(IEmailGenerator emailGenerator, IQueue<Queues.Models.MailMessage> queue, FormattingPluginManager pluginManager, IAppStatsClient statsClient) {
            _emailGenerator = emailGenerator;
            _queue = queue;
            _pluginManager = pluginManager;
            _statsClient = statsClient;
        }

        public void SendPasswordReset(User user) {
            if (user == null || String.IsNullOrEmpty(user.PasswordResetToken))
                return;

            MailMessage msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "PasswordReset");
            msg.To.Add(user.EmailAddress);
            QueueMessage(msg);
        }

        public void SendVerifyEmail(User user) {
            MailMessage msg = _emailGenerator.GenerateMessage(new UserModel {
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "VerifyEmail");
            msg.To.Add(user.EmailAddress);
            QueueMessage(msg);
        }

        public void SendInvite(User sender, Organization organization, Invite invite) {
            MailMessage msg = _emailGenerator.GenerateMessage(new InviteModel {
                Sender = sender,
                Organization = organization,
                Invite = invite,
                BaseUrl = Settings.Current.BaseURL
            }, "Invite");
            msg.To.Add(invite.EmailAddress);
            QueueMessage(msg);
        }

        public void SendPaymentFailed(User owner, Organization organization) {
            MailMessage msg = _emailGenerator.GenerateMessage(new PaymentModel {
                Owner = owner,
                Organization = organization,
                BaseUrl = Settings.Current.BaseURL
            }, "PaymentFailed");
            msg.To.Add(owner.EmailAddress);
            QueueMessage(msg);
        }

        public void SendAddedToOrganization(User sender, Organization organization, User user) {
            MailMessage msg = _emailGenerator.GenerateMessage(new AddedToOrganizationModel {
                Sender = sender,
                Organization = organization,
                User = user,
                BaseUrl = Settings.Current.BaseURL
            }, "AddedToOrganization");
            msg.To.Add(user.EmailAddress);

            QueueMessage(msg);
        }

        public void SendNotice(string emailAddress, EventNotification model) {
            var message = _pluginManager.GetEventNotificationMailMessage(model);
            if (message == null)
                return;

            message.To = emailAddress;
            QueueMessage(message.ToMailMessage());
        }

        public void SendSummaryNotification(string emailAddress, SummaryNotificationModel notification) {
            notification.BaseUrl = Settings.Current.BaseURL;
            MailMessage msg = _emailGenerator.GenerateMessage(notification, "SummaryNotification");
            msg.To.Add(emailAddress);
            QueueMessage(msg);
        }

        private void QueueMessage(MailMessage message) {
            CleanAddresses(message);

            _queue.Enqueue(message.ToMailMessage());
            _statsClient.Counter(StatNames.EmailsQueued);
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