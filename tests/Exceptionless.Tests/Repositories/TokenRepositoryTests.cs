using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Xunit;
using Xunit.Abstractions;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Tests.Repositories;

public sealed class TokenRepositoryTests : IntegrationTestsBase
{
    private readonly ITokenRepository _repository;

    public TokenRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _repository = GetService<ITokenRepository>();
    }

    [Fact]
    public async Task GetAndRemoveByProjectIdOrDefaultProjectIdAsync()
    {
        await _repository.AddAsync(new List<Token> {
                new() { OrganizationId = TestConstants.OrganizationId, CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime, UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime, Id = StringExtensions.GetNewToken() },
                new() { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId, CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime, UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime, Id = StringExtensions.GetNewToken() },
                new() { OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime, UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime, Id = StringExtensions.GetNewToken() },
                new() { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectId, CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime, UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime, Id = StringExtensions.GetNewToken() },
                new() { OrganizationId = TestConstants.OrganizationId, DefaultProjectId = TestConstants.ProjectIdWithNoRoles, CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime, UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime, Id = StringExtensions.GetNewToken() },
                new() { DefaultProjectId = TestConstants.ProjectIdWithNoRoles, UserId = TestConstants.UserId, CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime, UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime, Id = StringExtensions.GetNewToken() }
            }, o => o.ImmediateConsistency());

        Assert.Equal(5, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
        Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
        Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

        await _repository.RemoveAllByProjectIdAsync(TestConstants.OrganizationId, TestConstants.ProjectId);
        await RefreshDataAsync();

        Assert.Equal(4, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
        Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
        Assert.Equal(3, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

        await _repository.RemoveAllByProjectIdAsync(TestConstants.OrganizationId, TestConstants.ProjectIdWithNoRoles);
        await RefreshDataAsync();

        Assert.Equal(3, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
        Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
        Assert.Equal(2, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);

        await _repository.RemoveAllByOrganizationIdAsync(TestConstants.OrganizationId);
        await RefreshDataAsync();

        Assert.Equal(0, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
        Assert.Equal(0, (await _repository.GetByProjectIdAsync(TestConstants.ProjectId)).Total);
        Assert.Equal(1, (await _repository.GetByProjectIdAsync(TestConstants.ProjectIdWithNoRoles)).Total);
    }

    [Fact]
    public async Task GetAndRemoveByByUserIdAsync()
    {
        await _repository.AddAsync(new List<Token> {
                new() { OrganizationId = TestConstants.OrganizationId, CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime, UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime, Id = StringExtensions.GetNewToken(), Type = TokenType.Access },
                new() { OrganizationId = TestConstants.OrganizationId, UserId = TestConstants.UserId, CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime, UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime, Id = StringExtensions.GetNewToken(), Type = TokenType.Access },
                new() { OrganizationId = TestConstants.OrganizationId, UserId = TestConstants.UserId, CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime, UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime, Id = StringExtensions.GetNewToken(), Type = TokenType.Authentication }
            }, o => o.ImmediateConsistency());
        Assert.Equal(1, (await _repository.GetByTypeAndUserIdAsync(TokenType.Access, TestConstants.UserId)).Total);
        Assert.Equal(1, (await _repository.GetByTypeAndUserIdAsync(TokenType.Authentication, TestConstants.UserId)).Total);

        await _repository.RemoveAllByUserIdAsync(TestConstants.UserId, o => o.ImmediateConsistency());
        Assert.Equal(0, (await _repository.GetByTypeAndUserIdAsync(TokenType.Access, TestConstants.UserId)).Total);
        Assert.Equal(0, (await _repository.GetByTypeAndUserIdAsync(TokenType.Authentication, TestConstants.UserId)).Total);
        Assert.Equal(1, (await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Total);
    }
}
