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
            RemoveData();

            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { DefaultProjectId = TestConstants.ProjectIdWithNoRoles, UserId = TestConstants.UserId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            await _client.RefreshAsync();

            Assert.Equal(5, _repository.GetByOrganizationId(TestConstants.OrganizationId).Total);
            Assert.Equal(2, _repository.GetByProjectId(TestConstants.ProjectId).Total);
            Assert.Equal(3, _repository.GetByProjectId(TestConstants.ProjectIdWithNoRoles).Total);

            await _repository.RemoveAllByProjectIdsAsync(new []{ TestConstants.ProjectId });
            await _client.RefreshAsync();

            Assert.Equal(4, _repository.GetByOrganizationId(TestConstants.OrganizationId).Total);
            Assert.Equal(1, _repository.GetByProjectId(TestConstants.ProjectId).Total);
            Assert.Equal(3, _repository.GetByProjectId(TestConstants.ProjectIdWithNoRoles).Total);

            await _repository.RemoveAllByProjectIdsAsync(new[] { TestConstants.ProjectIdWithNoRoles });
            await _client.RefreshAsync();
            
            Assert.Equal(3, _repository.GetByOrganizationId(TestConstants.OrganizationId).Total);
            Assert.Equal(1, _repository.GetByProjectId(TestConstants.ProjectId).Total);
            Assert.Equal(2, _repository.GetByProjectId(TestConstants.ProjectIdWithNoRoles).Total);

            await _repository.RemoveAllByOrganizationIdsAsync(new[] { TestConstants.OrganizationId });
            await _client.RefreshAsync();

            Assert.Equal(0, _repository.GetByOrganizationId(TestConstants.OrganizationId).Total);
            Assert.Equal(0, _repository.GetByProjectId(TestConstants.ProjectId).Total);
            Assert.Equal(1, _repository.GetByProjectId(TestConstants.ProjectIdWithNoRoles).Total);
        }
        
        protected void RemoveData() {
            _repository.RemoveAll();
        }

        public void Dispose() {
            //RemoveData();
        }
    }
}