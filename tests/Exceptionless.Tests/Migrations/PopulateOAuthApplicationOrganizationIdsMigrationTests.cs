using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class PopulateOAuthApplicationOrganizationIdsMigrationTests : IntegrationTestsBase
{
    private readonly IOAuthApplicationRepository _oauthApplicationRepository;
    private readonly IOAuthTokenRepository _oauthTokenRepository;

    public PopulateOAuthApplicationOrganizationIdsMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _oauthApplicationRepository = GetService<IOAuthApplicationRepository>();
        _oauthTokenRepository = GetService<IOAuthTokenRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<PopulateOAuthApplicationOrganizationIds>();
        services.AddSingleton<ILock>(EmptyLock.Empty);
        base.RegisterServices(services);
    }

    [Fact]
    public async Task RunAsync_LegacyOAuthTokens_PopulatesDistinctApplicationOrganizations()
    {
        string clientId = $"legacy-oauth-{ObjectId.GenerateNewId()}";
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var application = await _oauthApplicationRepository.AddAsync(new OAuthApplication
        {
            ClientId = clientId,
            Name = "Legacy OAuth Application",
            RedirectUris = ["http://localhost/callback"],
            Scopes = [AuthorizationRoles.McpRead],
            CreatedByUserId = OAuthApplication.SystemUserId,
            UpdatedByUserId = OAuthApplication.SystemUserId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        }, options => options.ImmediateConsistency());
        await _oauthTokenRepository.AddAsync([
            CreateToken(clientId, [TestConstants.OrganizationId, TestConstants.OrganizationId2], utcNow),
            CreateToken(clientId, [TestConstants.OrganizationId], utcNow)
        ], options => options.ImmediateConsistency());

        var migration = GetService<PopulateOAuthApplicationOrganizationIds>();
        await migration.RunAsync(new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken));

        var migrated = await _oauthApplicationRepository.GetByIdAsync(application.Id, options => options.ImmediateConsistency());
        Assert.NotNull(migrated);
        Assert.Equal(2, migrated.OrganizationIds.Count);
        Assert.Contains(TestConstants.OrganizationId, migrated.OrganizationIds);
        Assert.Contains(TestConstants.OrganizationId2, migrated.OrganizationIds);
    }

    private static OAuthToken CreateToken(string clientId, IReadOnlyCollection<string> organizationIds, DateTime utcNow)
    {
        return new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = TestConstants.UserId,
            ClientId = clientId,
            GrantId = StringExtensions.GetNewToken(),
            Resource = "http://localhost/mcp",
            AccessTokenHash = StringExtensions.GetNewToken(),
            OrganizationIds = organizationIds.ToHashSet(StringComparer.Ordinal),
            Scopes = [AuthorizationRoles.McpRead],
            CreatedBy = TestConstants.UserId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };
    }
}
