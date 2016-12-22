using System;
using System.Diagnostics;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Messaging.Models {
    [DebuggerDisplay("{Type} {ChangeType}: Id={Id}, OrganizationId={OrganizationId}, ProjectId={ProjectId}, StackId={StackId}")]
    public class ExtendedEntityChanged : EntityChanged {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string StackId { get; set; }
    }
}