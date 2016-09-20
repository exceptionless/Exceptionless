using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IWebHookRepository : IRepositoryOwnedByOrganizationAndProject<WebHook> {
        Task<FindResults<WebHook>> GetByUrlAsync(string targetUrl);
        Task<FindResults<WebHook>> GetByOrganizationIdOrProjectIdAsync(string organizationId, string projectId);
    }
}