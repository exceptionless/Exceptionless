using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IWebHookRepository : IRepositoryOwnedByOrganizationAndProject<WebHook> {
        void RemoveByUrl(string targetUrl);
        FindResults<WebHook> GetByOrganizationIdOrProjectId(string organizationId, string projectId);
    }
}