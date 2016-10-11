using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class TokenRepositoryTests : ElasticTestBase {
        private readonly ITokenRepository _repository;

        public TokenRepositoryTests(ITestOutputHelper output) : base(output) {
            _repository = GetService<ITokenRepository>();
        }

        [Fact]
        public async Task GetAndRemoveByProjectIdOrDefaultProjectIdAsync() {
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, CreatedUtc = SystemClock.UtcNow, ModifiedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, CreatedUtc = SystemClock.UtcNow, ModifiedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = SystemClock.UtcNow, ModifiedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectId, CreatedUtc = SystemClock.UtcNow, ModifiedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = SystemClock.UtcNow, ModifiedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() });
            await _repository.AddAsync(new Token { DefaultProjectId = TestConstants.ProjectIdWithNoRoles, UserId = TestConstants.UserId, CreatedUtc = SystemClock.UtcNow, ModifiedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() });

            await _configuration.Client.RefreshAsync();
            Assert.Equal(5, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

            await _repository.RemoveAllByProjectIdAsync(TestConstants.OrganizationId, TestConstants.ProjectId);

            await _configuration.Client.RefreshAsync();
            Assert.Equal(4, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

            await _repository.RemoveAllByProjectIdAsync(TestConstants.OrganizationId, TestConstants.ProjectIdWithNoRoles);

            await _configuration.Client.RefreshAsync();
            Assert.Equal(3, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

            await _repository.RemoveAllByOrganizationIdAsync(TestConstants.OrganizationId);

            await _configuration.Client.RefreshAsync();
            Assert.Equal(0, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(0, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);
        }
    }
}