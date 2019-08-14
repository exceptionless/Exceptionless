using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Repositories {
    public sealed class WebHookRepositoryTests : IntegrationTestsBase {
        private readonly IWebHookRepository _repository;

        public WebHookRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _repository = GetService<IWebHookRepository>();
        }

        [Fact]
        public async Task GetByOrganizationIdOrProjectIdAsync() {
            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, Url = "http://localhost:40000/test", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(2, 0, 0, 0) });
            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, Url = "http://localhost:40000/test1", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(2, 0, 0, 0) });
            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, Url = "http://localhost:40000/test1", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(2, 0, 0, 0) });

            await RefreshDataAsync();
            Assert.Equal(3, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(2, (await _repository.GetByOrganizationIdOrProjectIdAsync(TestConstants.OrganizationId, TestConstants.ProjectId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);
        }

        [Fact]
        public async Task CanSaveWebHookVersionAsync() {
            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, Url = "http://localhost:40000/test", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(1, 1, 1, 1) });
            await _repository.AddAsync(new WebHook { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, Url = "http://localhost:40000/test1", EventTypes = new[] { WebHookRepository.EventTypes.StackPromoted }, Version = new Version(2, 2, 2, 2) });

            await RefreshDataAsync();
            Assert.Equal(new Version(1, 1, 1, 1), (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Documents.First().Version);
            Assert.Equal(new Version(2, 2, 2, 2), (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Documents.First().Version);
        }
    }
}