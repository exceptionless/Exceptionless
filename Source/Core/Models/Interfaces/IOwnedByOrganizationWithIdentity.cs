using System;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models {
    public interface IOwnedByOrganizationWithIdentity : IOwnedByOrganization, IIdentity {}
}