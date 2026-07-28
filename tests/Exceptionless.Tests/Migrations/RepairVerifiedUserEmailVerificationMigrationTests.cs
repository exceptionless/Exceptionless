using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public class RepairVerifiedUserEmailVerificationMigrationTests : IntegrationTestsBase
{
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly IUserRepository _userRepository;

    public RepairVerifiedUserEmailVerificationMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _configuration = GetService<ExceptionlessElasticConfiguration>();
        _userRepository = GetService<IUserRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<RepairVerifiedUserEmailVerification>();
        services.AddSingleton<ILock>(EmptyLock.Empty);
        base.RegisterServices(services);
    }

    [Fact]
    public async Task RunAsync_InvalidVerifiedUser_RepairsVerificationStateAndInvalidatesCache()
    {
        var invalidUser = new User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            OrganizationIds = new HashSet<string> { "5714ef0547abb90ddcf387c4" },
            Password = "password-hash",
            Salt = "password-salt",
            PasswordResetTokenExpiration = DateTime.MinValue,
            FullName = "Legacy User",
            EmailAddress = "legacy-user@example.com",
            EmailNotificationsEnabled = false,
            IsEmailAddressVerified = true,
            VerifyEmailAddressToken = "weZKyNQcmZ3bmH44UKukp1bUILcy3PmQsFkVVRR9",
            VerifyEmailAddressTokenExpiration = new DateTime(2020, 7, 7, 22, 31, 30, DateTimeKind.Utc),
            IsActive = true,
            Roles = new HashSet<string> { "client", "user" },
            CreatedUtc = new DateTime(2016, 7, 20, 18, 33, 49, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2023, 10, 23, 17, 13, 33, DateTimeKind.Utc)
        };

        var indexResponse = await _configuration.Client.IndexAsync(
            invalidUser,
            request => request
                .Index(_configuration.Users.VersionedName)
                .Id(invalidUser.Id)
                .Refresh(Refresh.WaitFor),
            TestCancellationToken);
        Assert.True(indexResponse.IsValidResponse, indexResponse.DebugInformation);

        var cachedUser = await _userRepository.GetByEmailAddressAsync(invalidUser.EmailAddress);
        Assert.NotNull(cachedUser);
        Assert.True(cachedUser.IsEmailAddressVerified);
        Assert.Equal(invalidUser.VerifyEmailAddressToken, cachedUser.VerifyEmailAddressToken);
        Assert.Equal(invalidUser.VerifyEmailAddressTokenExpiration, cachedUser.VerifyEmailAddressTokenExpiration);
        Assert.NotNull(await _userRepository.GetByIdAsync(invalidUser.Id));

        var migration = GetService<RepairVerifiedUserEmailVerification>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);

        var repairedUserById = await _userRepository.GetByIdAsync(invalidUser.Id);
        Assert.NotNull(repairedUserById);
        Assert.True(repairedUserById.IsEmailAddressVerified);
        Assert.Null(repairedUserById.VerifyEmailAddressToken);
        Assert.Equal(DateTime.MinValue, repairedUserById.VerifyEmailAddressTokenExpiration);

        var repairedUser = await _userRepository.GetByEmailAddressAsync(invalidUser.EmailAddress);
        Assert.NotNull(repairedUser);
        Assert.True(repairedUser.IsEmailAddressVerified);
        Assert.Null(repairedUser.VerifyEmailAddressToken);
        Assert.Equal(DateTime.MinValue, repairedUser.VerifyEmailAddressTokenExpiration);
        Assert.Equal(invalidUser.OrganizationIds, repairedUser.OrganizationIds);
        Assert.Equal(invalidUser.Roles, repairedUser.Roles);
        Assert.Equal(invalidUser.Password, repairedUser.Password);
        Assert.Equal(invalidUser.Salt, repairedUser.Salt);
        Assert.Equal(invalidUser.CreatedUtc, repairedUser.CreatedUtc);
        Assert.Equal(invalidUser.UpdatedUtc, repairedUser.UpdatedUtc);
        Assert.Equal(invalidUser.FullName, repairedUser.FullName);
        Assert.Equal(invalidUser.EmailNotificationsEnabled, repairedUser.EmailNotificationsEnabled);

        await migration.RunAsync(context);

        var repairedAgainUser = await _userRepository.GetByEmailAddressAsync(invalidUser.EmailAddress);
        Assert.NotNull(repairedAgainUser);
        Assert.True(repairedAgainUser.IsEmailAddressVerified);
        Assert.Null(repairedAgainUser.VerifyEmailAddressToken);
        Assert.Equal(DateTime.MinValue, repairedAgainUser.VerifyEmailAddressTokenExpiration);
    }

    [Fact]
    public async Task RunAsync_EachInvalidVerificationFieldCombination_RepairsEveryUser()
    {
        var tokenOnlyUser = new User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FullName = "Token Only User",
            EmailAddress = "token-only-user@example.com",
            IsEmailAddressVerified = true,
            VerifyEmailAddressToken = "stale-token",
            VerifyEmailAddressTokenExpiration = DateTime.MinValue
        };
        var expirationOnlyUser = new User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FullName = "Expiration Only User",
            EmailAddress = "expiration-only-user@example.com",
            IsEmailAddressVerified = true,
            VerifyEmailAddressToken = null,
            VerifyEmailAddressTokenExpiration = new DateTime(2020, 7, 7, 22, 31, 30, DateTimeKind.Utc)
        };

        await Task.WhenAll(IndexUserAsync(tokenOnlyUser), IndexUserAsync(expirationOnlyUser));

        var migration = GetService<RepairVerifiedUserEmailVerification>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);

        var repairedUsers = await _userRepository.GetByIdsAsync([tokenOnlyUser.Id, expirationOnlyUser.Id]);
        Assert.Equal(2, repairedUsers.Count);
        Assert.All(repairedUsers, user =>
        {
            Assert.True(user.IsEmailAddressVerified);
            Assert.Null(user.VerifyEmailAddressToken);
            Assert.Equal(DateTime.MinValue, user.VerifyEmailAddressTokenExpiration);
        });
    }

    [Fact]
    public async Task RunAsync_ValidUsers_PreservesVerificationState()
    {
        var verifiedUser = new User
        {
            FullName = "Verified User",
            EmailAddress = "verified-user@example.com",
            Roles = new HashSet<string> { "client", "user" }
        };
        verifiedUser.MarkEmailAddressVerified();

        var unverifiedUser = new User
        {
            FullName = "Unverified User",
            EmailAddress = "unverified-user@example.com",
            Roles = new HashSet<string> { "client", "user" }
        };
        unverifiedUser.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);

        await _userRepository.AddAsync([verifiedUser, unverifiedUser], o => o.ImmediateConsistency());

        string? originalUnverifiedToken = unverifiedUser.VerifyEmailAddressToken;
        DateTime originalUnverifiedExpiration = unverifiedUser.VerifyEmailAddressTokenExpiration;
        var verifiedBeforeMigration = await _configuration.Client.GetAsync<User>(
            _configuration.Users.VersionedName,
            verifiedUser.Id,
            TestCancellationToken);
        var unverifiedBeforeMigration = await _configuration.Client.GetAsync<User>(
            _configuration.Users.VersionedName,
            unverifiedUser.Id,
            TestCancellationToken);

        var migration = GetService<RepairVerifiedUserEmailVerification>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);

        var verifiedAfterMigration = await _configuration.Client.GetAsync<User>(
            _configuration.Users.VersionedName,
            verifiedUser.Id,
            TestCancellationToken);
        var unverifiedAfterMigration = await _configuration.Client.GetAsync<User>(
            _configuration.Users.VersionedName,
            unverifiedUser.Id,
            TestCancellationToken);

        Assert.Equal(verifiedBeforeMigration.Version, verifiedAfterMigration.Version);
        Assert.Equal(unverifiedBeforeMigration.Version, unverifiedAfterMigration.Version);

        var repairedVerifiedUser = await _userRepository.GetByEmailAddressAsync(verifiedUser.EmailAddress);
        Assert.NotNull(repairedVerifiedUser);
        Assert.True(repairedVerifiedUser.IsEmailAddressVerified);
        Assert.Null(repairedVerifiedUser.VerifyEmailAddressToken);
        Assert.Equal(DateTime.MinValue, repairedVerifiedUser.VerifyEmailAddressTokenExpiration);

        var unchangedUnverifiedUser = await _userRepository.GetByEmailAddressAsync(unverifiedUser.EmailAddress);
        Assert.NotNull(unchangedUnverifiedUser);
        Assert.False(unchangedUnverifiedUser.IsEmailAddressVerified);
        Assert.Equal(originalUnverifiedToken, unchangedUnverifiedUser.VerifyEmailAddressToken);
        Assert.Equal(originalUnverifiedExpiration, unchangedUnverifiedUser.VerifyEmailAddressTokenExpiration);
    }

    private async Task IndexUserAsync(User user)
    {
        var response = await _configuration.Client.IndexAsync(
            user,
            request => request
                .Index(_configuration.Users.VersionedName)
                .Id(user.Id)
                .Refresh(Refresh.WaitFor),
            TestCancellationToken);

        Assert.True(response.IsValidResponse, response.DebugInformation);
    }
}
