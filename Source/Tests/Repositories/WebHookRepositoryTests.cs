using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Utility;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class WebHookRepositoryTests : IDisposable {
        public readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IWebHookRepository _repository = IoC.GetInstance<IWebHookRepository>();

        [Fact]
        public async Task GetByOrganizationIdOrProjectIdAsync() {
            await RemoveDataAsync();

            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, Url = "http://localhost:40000/test", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(2, 0, 0, 0) });
            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, Url = "http://localhost:40000/test1", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(2, 0, 0, 0) });
            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, Url = "http://localhost:40000/test1", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(2, 0, 0, 0) });
            await _client.RefreshAsync(Indices.All);

            Assert.Equal(3, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(2, (await _repository.GetByOrganizationIdOrProjectIdAsync(TestConstants.OrganizationId, TestConstants.ProjectId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);
        }
        
        [Fact]
        public async Task CanSaveWebHookVersionAsync() {
            await RemoveDataAsync();

            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, Url = "http://localhost:40000/test", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(1, 1, 1, 1) });
            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, Url = "http://localhost:40000/test1", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(2, 2, 2, 2) });
            await _client.RefreshAsync(Indices.All);

            Assert.Equal(new Version(1, 1, 1, 1), (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Documents.First().Version);
            Assert.Equal(new Version(2, 2, 2, 2), (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Documents.First().Version);
        }

        protected async Task RemoveDataAsync() {
            await _repository.RemoveAllAsync();
            await _client.RefreshAsync(Indices.All);
        }

        public void Dispose() {
            //await RemoveDataAsync();
        }
    }
}