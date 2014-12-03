#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Models;

namespace Exceptionless.Core.Mail {
    public interface IMailer {
        void SendPasswordReset(User user);
        void SendVerifyEmail(User user);
        void SendInvite(User sender, Organization organization, Invite invite);
        void SendPaymentFailed(User owner, Organization organization);
        void SendAddedToOrganization(User sender, Organization organization, User user);
        void SendNotice(string emailAddress, EventNotification model);
        void SendSummaryNotification(string emailAddress, SummaryNotificationModel notification);
    }
}