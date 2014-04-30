#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Models.Data;

namespace Exceptionless.Core.Mail.Models {
    public class EventNotificationModel : EventNotification, IMailModel {
        public EventNotificationModel() {}

        public EventNotificationModel(EventNotification notification) {
            Event = notification.Event;
            IsNew = notification.IsNew;
            IsCritical = notification.IsCritical;
            IsRegression = notification.IsRegression;
            TotalOccurrences = notification.TotalOccurrences;
        }

        public string ProjectName { get; set; }
        public string Subject { get; set; }
        public string BaseUrl { get; set; }
        public string NotificationType { get; set; }
        public InnerError Error { get; set; }
        public string Url { get; set; }
        public Method Method { get; set; }

        public string TypeName {
            get {
                if (String.IsNullOrEmpty(FullTypeName))
                    return String.Empty;

                string[] parts = FullTypeName.Split('.');
                return parts[parts.Length - 1];
            }
        }

        public string Message { get { return Error != null && !String.IsNullOrWhiteSpace(Error.Message) ? Error.Message : "(None)"; } }
        public string FullTypeName { get { return Error != null && !String.IsNullOrWhiteSpace(Error.Type) ? Error.Type : "(None)"; } }
        public string MethodName { get { return Method != null && !String.IsNullOrWhiteSpace(Method.GetFullName()) ? Method.GetFullName() : "(None)"; } }
    }
}