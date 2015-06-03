using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models.Admin;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class TokenRepositoryTests : IDisposable {
        private readonly ITokenRepository _repository = IoC.GetInstance<ITokenRepository>();

        [Fact]
        public async Task GetAndRemoveByProjectIdOrDefaultProjectIdAsync() {
            RemoveData();

            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });
            _repository.Add(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, Id = StringExtensions.GetNewToken() });

            Assert.Equal(6, _repository.GetByOrganizationId(TestConstants.OrganizationId).Count);
            Assert.Equal(3, _repository.GetByProjectId(TestConstants.ProjectId).Count);
            Assert.Equal(2, _repository.GetByProjectId(TestConstants.ProjectIdWithNoRoles).Count);

            await _repository.RemoveAllByProjectIdsAsync(new []{ TestConstants.ProjectId });
            Assert.Equal(3, _repository.GetByOrganizationId(TestConstants.OrganizationId).Count);
            Assert.Equal(0, _repository.GetByProjectId(TestConstants.ProjectId).Count);
            Assert.Equal(2, _repository.GetByProjectId(TestConstants.ProjectIdWithNoRoles).Count);

            await _repository.RemoveAllByProjectIdsAsync(new[] { TestConstants.ProjectIdWithNoRoles });
            Assert.Equal(1, _repository.GetByOrganizationId(TestConstants.OrganizationId).Count);
            Assert.Equal(0, _repository.GetByProjectId(TestConstants.ProjectId).Count);
            Assert.Equal(0, _repository.GetByProjectId(TestConstants.ProjectIdWithNoRoles).Count);

            await _repository.RemoveAllByOrganizationIdsAsync(new[] { TestConstants.OrganizationId });
            Assert.Equal(0, _repository.GetByOrganizationId(TestConstants.OrganizationId).Count);
            Assert.Equal(0, _repository.GetByProjectId(TestConstants.ProjectId).Count);
            Assert.Equal(0, _repository.GetByProjectId(TestConstants.ProjectIdWithNoRoles).Count);
        }

        protected void RemoveData() {
            _repository.RemoveAll();
        }

        public void Dispose() {
            //RemoveData();
        }
    }
}