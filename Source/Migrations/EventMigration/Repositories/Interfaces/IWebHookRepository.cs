using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.EventMigration.Repositories {
    public interface IWebHookRepository : IRepositoryOwnedByOrganizationAndProject<WebHook> {
        void RemoveByUrl(string targetUrl);
        ICollection<WebHook> GetByOrganizationIdOrProjectId(string organizationId, string projectId);
    }
}