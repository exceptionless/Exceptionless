using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Api.Models {
    public class ViewProject : IIdentity {
        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public string Name { get; set; }
        public bool DeleteBotDataEnabled { get; set; }
        public DataDictionary Data { get; set; }
        public HashSet<string> PromotedTabs { get; set; }
        public long StackCount { get; set; }
        public long EventCount { get; set; }
        public long TotalEventCount { get; set; }
        public DateTime LastEventDate { get; set; }
    }
}