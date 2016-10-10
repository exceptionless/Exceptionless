using System;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Messaging.Models {
    public class UserMembershipChanged {
        public ChangeType ChangeType { get; set; }
        public string UserId { get; set; }
        public string OrganizationId { get; set; }
    }
}
