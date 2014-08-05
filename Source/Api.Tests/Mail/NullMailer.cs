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
using Exceptionless.Core.Queues.Models;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Mail {
    public class NullMailer : IMailer {
        public Task SendPasswordResetAsync(User user) {
            return Task.Delay(0);
        }

        public Task SendVerifyEmailAsync(User user) {
            return Task.Delay(0);
        }

        public Task SendInviteAsync(User sender, Organization organization, Invite invite) {
            return Task.Delay(0);
        }

        public Task SendPaymentFailedAsync(User owner, Organization organization) {
            return Task.Delay(0);
        }

        public Task SendAddedToOrganizationAsync(User sender, Organization organization, User user) {
            return Task.Delay(0);
        }

        public Task SendNoticeAsync(string emailAddress, EventNotification model) {
            return Task.Delay(0);
        }

        public Task SendSummaryNotificationAsync(string emailAddress, SummaryNotificationModel notification) {
            return Task.Delay(0);
        }
    }
}