using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Options;
using Xunit;

namespace Exceptionless.Tests.Repositories;

public sealed class TokenAuthenticationRepositoryTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Theory]
    [InlineData(TokenType.Authentication, false)]
    [InlineData(TokenType.Authentication, true)]
    [InlineData(TokenType.Access, false)]
    [InlineData(TokenType.Access, true)]
    public async Task GetForAuthenticationAsync_LateUserTokenCacheWrite_ReadsCurrentStateOnce(TokenType type, bool deleted)
    {
        // Arrange
        var repository = CreateRepository();
        string id = Guid.NewGuid().ToString("N");
        var stale = new Token { Id = id, Type = type, UserId = "user" };
        var current = deleted ? null : new Token { Id = id, Type = type, UserId = "user", IsDisabled = true };
        repository.StoredToken = current;
        await repository.CacheTokenAsync(stale);

        // Act
        var result = await repository.GetForAuthenticationAsync(id);

        // Assert
        Assert.Same(current, result);
        Assert.Equal(1, repository.StorageReads);
        Assert.False(repository.ReadCache);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetForAuthenticationAsync_ColdToken_ReadsOnceAndCachesOnlyProjectKeys(bool userToken)
    {
        // Arrange
        var repository = CreateRepository();
        var token = new Token { Id = Guid.NewGuid().ToString("N"), Type = TokenType.Access, UserId = userToken ? "user" : null };
        repository.StoredToken = token;

        // Act
        var first = await repository.GetForAuthenticationAsync(token.Id);
        int firstReadCount = repository.StorageReads;
        var second = await repository.GetForAuthenticationAsync(token.Id);

        // Assert
        Assert.Equal(token.Id, first?.Id);
        Assert.Equal(token.Id, second?.Id);
        Assert.Equal(1, firstReadCount);
        Assert.Equal(userToken ? 2 : 1, repository.StorageReads);
        Assert.False(repository.ReadCache);
    }

    private StorageStubTokenRepository CreateRepository() => new(GetService<ExceptionlessElasticConfiguration>(), GetService<MiniValidationValidator>(), GetService<AppOptions>());

    private sealed class StorageStubTokenRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : TokenRepository(configuration, validator, options)
    {
        public Token? StoredToken { get; set; }
        public int StorageReads { get; private set; }
        public bool ReadCache { get; private set; }

        public override Task<Token?> GetByIdAsync(Id id, ICommandOptions? options = null)
        {
            StorageReads++;
            ReadCache = options?.ShouldReadCache() == true;
            return Task.FromResult(StoredToken);
        }

        public Task CacheTokenAsync(Token token) => AddDocumentsToCacheAsync(token, new CommandOptions<Token>().Cache(), false);
    }
}
