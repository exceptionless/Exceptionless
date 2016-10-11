using System;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Messaging.Models {
    public class ExtendedEntityChanged : EntityChanged {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string StackId { get; set; }
    }
}
