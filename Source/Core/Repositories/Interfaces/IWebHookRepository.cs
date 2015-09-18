using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IWebHookRepository : IRepositoryOwnedByOrganizationAndProject<WebHook> {
        Task RemoveByUrlAsync(string targetUrl);
        Task<FindResults<WebHook>> GetByOrganizationIdOrProjectIdAsync(string organizationId, string projectId);
    }
}