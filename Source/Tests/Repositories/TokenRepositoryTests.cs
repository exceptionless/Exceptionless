using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Nest;
using Xunit;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Api.Tests.Repositories {
    public class TokenRepositoryTests : IDisposable {
        public readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly ITokenRepository _repository = IoC.GetInstance<ITokenRepository>();

        [Fact]
        public async Task GetAndRemoveByProjectIdOrDefaultProjectIdAsync() {
            await RemoveDataAsync();

            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { DefaultProjectId = TestConstants.ProjectIdWithNoRoles, UserId = TestConstants.UserId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            await _client.RefreshAsync(Indices.All);

            Assert.Equal(5, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

            await _repository.RemoveAllByProjectIdsAsync(new []{ TestConstants.ProjectId });
            await _client.RefreshAsync(Indices.All);

            Assert.Equal(4, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

            await _repository.RemoveAllByProjectIdsAsync(new[] { TestConstants.ProjectIdWithNoRoles });
            await _client.RefreshAsync(Indices.All);

            Assert.Equal(3, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

            await _repository.RemoveAllByOrganizationIdsAsync(new[] { TestConstants.OrganizationId });
            await _client.RefreshAsync(Indices.All);

            Assert.Equal(0, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(0, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);
        }

        protected async Task RemoveDataAsync() {
            await _repository.RemoveAllAsync();
            await _client.RefreshAsync(Indices.All);
        }

        public async void Dispose() {
            await RemoveDataAsync();
        }
    }
}
