#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Threading.Tasks;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Models;

namespace Exceptionless.Tests.Utility {
    public class NullMailer : IMailer {
        public void SendPasswordReset(User user) {}

        public Task SendPasswordResetAsync(User sender) {
            return Task.Delay(0);
        }

        public void SendVerifyEmail(User user) {}

        public Task SendVerifyEmailAsync(User user) {
            return Task.Delay(0);
        }

        public void SendInvite(User sender, Organization organization, Invite invite) {}

        public Task SendInviteAsync(User sender, Organization organization, Invite invite) {
            return Task.Delay(0);
        }

        public Task SendPaymentFailedAsync(User owner, Organization organization) {
            return Task.Delay(0);
        }

        public void SendAddedToOrganization(User sender, Organization organization, User user) {}

        public Task SendAddedToOrganizationAsync(User sender, Organization organization, User user) {
            return Task.Delay(0);
        }

        public void SendNotice(string emailAddress, EventNotificationModel notification) {}

        public Task SendNoticeAsync(string emailAddress, EventNotificationModel notification) {
            return Task.Delay(0);
        }

        public void SendSummaryNotification(string emailAddress, SummaryNotificationModel notification) {}

        public Task SendSummaryNotificationAsync(string emailAddress, SummaryNotificationModel notification) {
            return Task.Delay(0);
        }

        public void SendPaymentFailed(User owner, Organization organization) {}
    }
}