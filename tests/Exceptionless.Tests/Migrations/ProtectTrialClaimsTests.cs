using System.Security.Cryptography;
using System.Text;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class ProtectTrialClaimsTests : IntegrationTestsBase
{
    public ProtectTrialClaimsTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    [Fact]
    public async Task RunAsync_LegacyClaimsAcrossPages_ProtectsHistoryAndCanResume()
    {
        // Arrange
        var repository = GetService<ITrialClaimRepository>();
        var options = GetService<AppOptions>();
        var createdUtc = TimeProvider.GetUtcNow().UtcDateTime.AddDays(-30);
        var legacy = Enumerable.Range(0, 101).Select(index => new TrialClaim
        {
            Id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"legacy-{index}@example.test"))),
            CreatedUtc = createdUtc
        }).ToArray();
        await repository.AddAsync(legacy, command => command.ImmediateConsistency());
        string resumedId = TrialEmailAddress.ProtectLegacyClaimId(legacy[0].Id, options.BillingOptions.TrialClaimSecret!);
        await repository.AddAsync(new TrialClaim { Id = resumedId, CreatedUtc = createdUtc }, command => command.ImmediateConsistency());
        var migration = new ProtectTrialClaims(repository, GetService<ExceptionlessElasticConfiguration>(), options, Log);
        var context = new MigrationContext(EmptyLock.Empty, _logger, TestCancellationToken);

        // Act
        await migration.RunAsync(context);
        await RefreshDataAsync();
        await migration.RunAsync(context);
        var claims = await repository.GetAllAsync(command => command.PageLimit(200));
        bool eligible = await GetService<BillingManager>().CanStartTrialAsync(new User { Id = TestConstants.UserId, EmailAddress = "legacy-0@example.test" });

        // Assert
        Assert.Equal(legacy.Length, claims.Documents.Count);
        Assert.All(claims.Documents, claim =>
        {
            Assert.StartsWith("hmac-v1-", claim.Id);
            Assert.Equal(createdUtc, claim.CreatedUtc);
        });
        Assert.False(eligible);
    }

    [Fact]
    public async Task RunAsync_MissingSecret_DoesNotEraseLegacyClaim()
    {
        // Arrange
        var repository = GetService<ITrialClaimRepository>();
        var claim = new TrialClaim { Id = new string('a', 64) };
        await repository.AddAsync(claim, command => command.ImmediateConsistency());
        var unconfigured = new AppOptions { BillingOptions = new Core.Configuration.BillingOptions() };
        var migration = new ProtectTrialClaims(repository, GetService<ExceptionlessElasticConfiguration>(), unconfigured, Log);

        // Act
        var exception = await Record.ExceptionAsync(() => migration.RunAsync(new MigrationContext(EmptyLock.Empty, _logger, TestCancellationToken)));
        bool retained = await repository.ExistsAsync(claim.Id);

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.True(retained);
    }
}
