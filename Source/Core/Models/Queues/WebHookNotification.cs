#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Core.Queues.Models {
    public class WebHookNotification {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string Url { get; set; }
        public object Data { get; set; }
    }
}