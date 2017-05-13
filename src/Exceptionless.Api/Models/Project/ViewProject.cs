using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Models;

namespace Exceptionless.Api.Models {
    public class ViewProject : IIdentity, IData, IHaveCreatedDate {
        public string Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public string Name { get; set; }
        public bool DeleteBotDataEnabled { get; set; }
        public DataDictionary Data { get; set; }
        public HashSet<string> PromotedTabs { get; set; }
        public bool? IsConfigured { get; set; }
        public long StackCount { get; set; }
        public long EventCount { get; set; }
        public bool HasPremiumFeatures { get; set; }
        public bool HasSlackIntegration { get; set; }
        public ICollection<UsageInfo> OverageHours { get; set; }
        public ICollection<UsageInfo> Usage { get; set; }
    }
}