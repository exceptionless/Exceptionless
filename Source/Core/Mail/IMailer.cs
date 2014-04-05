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
using Exceptionless.Models;

namespace Exceptionless.Core.Mail {
    public interface IMailer {
        void SendPasswordReset(User user);

        Task SendPasswordResetAsync(User sender);

        void SendVerifyEmail(User user);

        Task SendVerifyEmailAsync(User user);

        void SendInvite(User sender, Organization organization, Invite invite);

        Task SendInviteAsync(User sender, Organization organization, Invite invite);

        void SendPaymentFailed(User owner, Organization organization);

        Task SendPaymentFailedAsync(User owner, Organization organization);

        void SendAddedToOrganization(User sender, Organization organization, User user);

        Task SendAddedToOrganizationAsync(User sender, Organization organization, User user);

        void SendNotice(string emailAddress, EventNotificationModel notification);

        Task SendNoticeAsync(string emailAddress, EventNotificationModel notification);

        void SendSummaryNotification(string emailAddress, SummaryNotificationModel notification);

        Task SendSummaryNotificationAsync(string emailAddress, SummaryNotificationModel notification);
    }
}