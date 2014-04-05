#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Queues;

namespace Exceptionless.Core.Mail.Models {
    public class EventNotificationModel : EventNotification, IMailModel {
        public EventNotificationModel() {}

        public EventNotificationModel(EventNotification notification) {
            EventId = notification.EventId;
            StackId = notification.StackId;
            FullTypeName = notification.FullTypeName;
            IsNew = notification.IsNew;
            IsCritical = notification.IsCritical;
            IsRegression = notification.IsRegression;
            Message = notification.Message;
            ProjectId = notification.ProjectId;
            Url = notification.Url;
        }

        public string ProjectName { get; set; }
        public string Subject { get; set; }
        public string BaseUrl { get; set; }
        public int TotalOccurrences { get; set; }
        public string NotificationType { get; set; }
    }
}