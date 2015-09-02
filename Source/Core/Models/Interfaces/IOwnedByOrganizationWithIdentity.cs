using System;

namespace Exceptionless.Core.Models {
    public interface IOwnedByOrganizationWithIdentity : IOwnedByOrganization, IIdentity {}
}