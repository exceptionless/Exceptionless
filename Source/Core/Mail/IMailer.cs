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
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Models;

namespace Exceptionless.Core.Mail {
    public interface IMailer {
        Task SendPasswordResetAsync(User user);

        Task SendVerifyEmailAsync(User user);

        Task SendInviteAsync(User sender, Organization organization, Invite invite);

        Task SendPaymentFailedAsync(User owner, Organization organization);

        Task SendAddedToOrganizationAsync(User sender, Organization organization, User user);

        Task SendNoticeAsync(string emailAddress, EventNotification model);

        Task SendSummaryNotificationAsync(string emailAddress, SummaryNotificationModel notification);
    }
}