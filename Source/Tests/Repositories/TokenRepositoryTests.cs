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
            await RemoveDataAsync().AnyContext();

            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() }).AnyContext();
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() }).AnyContext();
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() }).AnyContext();
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() }).AnyContext();
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() }).AnyContext();
            await _repository.AddAsync(new Token { DefaultProjectId = TestConstants.ProjectIdWithNoRoles, UserId = TestConstants.UserId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() }).AnyContext();
            await _client.RefreshAsync().AnyContext();

            Assert.Equal(5, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId).AnyContext()).Total);
            Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId).AnyContext()).Total);
            Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles).AnyContext()).Total);

            await _repository.RemoveAllByProjectIdsAsync(new []{ TestConstants.ProjectId }).AnyContext();
            await _client.RefreshAsync().AnyContext();

            Assert.Equal(4, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId).AnyContext()).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId).AnyContext()).Total);
            Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles).AnyContext()).Total);

            await _repository.RemoveAllByProjectIdsAsync(new[] { TestConstants.ProjectIdWithNoRoles }).AnyContext();
            await _client.RefreshAsync().AnyContext();
            
            Assert.Equal(3, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId).AnyContext()).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId).AnyContext()).Total);
            Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles).AnyContext()).Total);

            await _repository.RemoveAllByOrganizationIdsAsync(new[] { TestConstants.OrganizationId }).AnyContext();
            await _client.RefreshAsync().AnyContext();

            Assert.Equal(0, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId).AnyContext()).Total);
            Assert.Equal(0, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId).AnyContext()).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles).AnyContext()).Total);
        }
        
        protected Task RemoveDataAsync() {
            return _repository.RemoveAllAsync();
        }

        public void Dispose() {
            //await RemoveDataAsync().AnyContext();
        }
    }
}