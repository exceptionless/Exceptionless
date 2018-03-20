using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Utility;
using Nest;
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
            await _repository.AddAsync(new List<Token> {
                new Token { OrganizationId = TestConstants.OrganizationId, CreatedUtc = SystemClock.UtcNow, UpdatedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() },
                new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, CreatedUtc = SystemClock.UtcNow, UpdatedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() },
                new Token { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = SystemClock.UtcNow, UpdatedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() },
                new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectId, CreatedUtc = SystemClock.UtcNow, UpdatedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() },
                new Token { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = SystemClock.UtcNow, UpdatedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() },
                new Token { DefaultProjectId = TestConstants.ProjectIdWithNoRoles, UserId = TestConstants.UserId, CreatedUtc = SystemClock.UtcNow, UpdatedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken() }
            }, o => o.ImmediateConsistency());

            Assert.Equal(5, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

            await _repository.RemoveAllByProjectIdAsync(TestConstants.OrganizationId, TestConstants.ProjectId);

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(4, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

            await _repository.RemoveAllByProjectIdAsync(TestConstants.OrganizationId, TestConstants.ProjectIdWithNoRoles);

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(3, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

            await _repository.RemoveAllByOrganizationIdAsync(TestConstants.OrganizationId);

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(0, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
            Assert.Equal(0, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
            Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);
        }

        [Fact]
        public async Task GetAndRemoveByByUserIdAsync() {
            await _repository.AddAsync(new List<Token> {
                new Token { OrganizationId = TestConstants.OrganizationId, CreatedUtc = SystemClock.UtcNow, UpdatedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken(), Type = TokenType.Access },
                new Token { OrganizationId = TestConstants.OrganizationId, UserId = TestConstants.UserId, CreatedUtc = SystemClock.UtcNow, UpdatedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken(), Type = TokenType.Access },
                new Token { OrganizationId = TestConstants.OrganizationId, UserId = TestConstants.UserId, CreatedUtc = SystemClock.UtcNow, UpdatedUtc = SystemClock.UtcNow, Id = StringExtensions.GetNewToken(), Type = TokenType.Authentication }
            }, o => o.ImmediateConsistency());
            Assert.Equal(1, (await _repository.GetByTypeAndUserIdAsync(TokenType.Access, TestConstants.UserId)).Total);
            Assert.Equal(1, (await _repository.GetByTypeAndUserIdAsync(TokenType.Authentication, TestConstants.UserId)).Total);

            await _repository.RemoveAllByUserIdAsync(TestConstants.UserId);
            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(0, (await _repository.GetByTypeAndUserIdAsync(TokenType.Access, TestConstants.UserId)).Total);
            Assert.Equal(0, (await _repository.GetByTypeAndUserIdAsync(TokenType.Authentication, TestConstants.UserId)).Total);
            Assert.Equal(1, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
        }
    }
}