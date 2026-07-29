using Exceptionless.Core.Extensions;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Lock;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public class RepairVerifiedUserEmailVerificationMigrationTests : IntegrationTestsBase
{
    private readonly IUserRepository _userRepository;

    public RepairVerifiedUserEmailVerificationMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _userRepository = GetService<IUserRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<RepairVerifiedUserEmailVerificationMigration>();
        services.AddSingleton<ILock>(EmptyLock.Empty);
        base.RegisterServices(services);
    }

    [Fact]
    public async Task RunAsync_InvalidVerifiedUser_RepairsVerificationStateAndInvalidatesCache()
    {
        // Arrange
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
            IsActive = true,
            Roles = new HashSet<string> { "client", "user" },
            CreatedUtc = new DateTime(2016, 7, 20, 18, 33, 49, DateTimeKind.Utc),
            UpdatedUtc = new DateTime(2023, 10, 23, 17, 13, 33, DateTimeKind.Utc)
        };
        const string staleToken = "weZKyNQcmZ3bmH44UKukp1bUILcy3PmQsFkVVRR9";
        var staleExpiration = new DateTime(2020, 7, 7, 22, 31, 30, DateTimeKind.Utc);
        await AddInvalidVerifiedUserAsync(invalidUser, staleToken, staleExpiration);

        var cachedUserById = await _userRepository.GetByIdAsync(invalidUser.Id);
        Assert.NotNull(cachedUserById);
        Assert.True(cachedUserById.IsEmailAddressVerified);
        Assert.Equal(staleToken, cachedUserById.VerifyEmailAddressToken);
        Assert.Equal(staleExpiration, cachedUserById.VerifyEmailAddressTokenExpiration);

        var cachedUserByEmail = await _userRepository.GetByEmailAddressAsync(invalidUser.EmailAddress);
        Assert.NotNull(cachedUserByEmail);
        Assert.Equal(staleToken, cachedUserByEmail.VerifyEmailAddressToken);
        DateTime updatedBeforeMigration = cachedUserByEmail.UpdatedUtc;

        var migration = GetService<RepairVerifiedUserEmailVerificationMigration>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);

        // Act
        await migration.RunAsync(context);

        // Assert
        var repairedUserById = await _userRepository.GetByIdAsync(invalidUser.Id);
        Assert.NotNull(repairedUserById);
        Assert.True(repairedUserById.IsEmailAddressVerified);
        Assert.Null(repairedUserById.VerifyEmailAddressToken);
        Assert.Equal(DateTime.MinValue, repairedUserById.VerifyEmailAddressTokenExpiration);

        var repairedUserByEmail = await _userRepository.GetByEmailAddressAsync(invalidUser.EmailAddress);
        Assert.NotNull(repairedUserByEmail);
        Assert.True(repairedUserByEmail.IsEmailAddressVerified);
        Assert.Null(repairedUserByEmail.VerifyEmailAddressToken);
        Assert.Equal(DateTime.MinValue, repairedUserByEmail.VerifyEmailAddressTokenExpiration);
        Assert.Equal(invalidUser.OrganizationIds, repairedUserByEmail.OrganizationIds);
        Assert.Equal(invalidUser.Roles, repairedUserByEmail.Roles);
        Assert.Equal(invalidUser.Password, repairedUserByEmail.Password);
        Assert.Equal(invalidUser.Salt, repairedUserByEmail.Salt);
        Assert.Equal(invalidUser.CreatedUtc, repairedUserByEmail.CreatedUtc);
        Assert.True(repairedUserByEmail.UpdatedUtc > updatedBeforeMigration);
        Assert.Equal(invalidUser.FullName, repairedUserByEmail.FullName);
        Assert.Equal(invalidUser.EmailNotificationsEnabled, repairedUserByEmail.EmailNotificationsEnabled);
    }

    [Fact]
    public async Task RunAsync_EachInvalidVerificationFieldCombination_RepairsEveryUser()
    {
        // Arrange
        var tokenOnlyUser = new User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FullName = "Token Only User",
            EmailAddress = "token-only-user@example.com"
        };
        var expirationOnlyUser = new User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FullName = "Expiration Only User",
            EmailAddress = "expiration-only-user@example.com"
        };
        var staleExpiration = new DateTime(2020, 7, 7, 22, 31, 30, DateTimeKind.Utc);

        await Task.WhenAll(
            AddInvalidVerifiedUserAsync(tokenOnlyUser, "stale-token", DateTime.MinValue),
            AddInvalidVerifiedUserAsync(expirationOnlyUser, null, staleExpiration));

        var migration = GetService<RepairVerifiedUserEmailVerificationMigration>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);

        // Act
        await migration.RunAsync(context);

        // Assert
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
    public async Task RunAsync_MoreUsersThanOneBatch_RepairsEveryUser()
    {
        // Arrange
        var users = Enumerable.Range(0, 101)
            .Select(index => new User
            {
                Id = ObjectId.GenerateNewId().ToString(),
                FullName = $"Paged User {index}",
                EmailAddress = $"paged-user-{index}@example.com"
            })
            .ToList();
        foreach (var user in users)
            user.MarkEmailAddressVerified();

        await _userRepository.AddAsync(users);
        long patchedUsers = await _userRepository.PatchAsync(
            users.Select(user => user.Id).ToArray(),
            new PartialPatch(new
            {
                verify_email_address_token = "stale-token",
                verify_email_address_token_expiration = new DateTime(2020, 7, 7, 22, 31, 30, DateTimeKind.Utc)
            }));
        Assert.Equal(users.Count, patchedUsers);

        var migration = GetService<RepairVerifiedUserEmailVerificationMigration>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);

        // Act
        await migration.RunAsync(context);

        // Assert
        var repairedUsers = await _userRepository.GetByIdsAsync(users.Select(user => user.Id).ToArray());
        Assert.Equal(users.Count, repairedUsers.Count);
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
        // Arrange
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
        await _userRepository.AddAsync([verifiedUser, unverifiedUser]);

        DateTime verifiedUpdatedBeforeMigration = verifiedUser.UpdatedUtc;
        DateTime unverifiedUpdatedBeforeMigration = unverifiedUser.UpdatedUtc;
        string? originalUnverifiedToken = unverifiedUser.VerifyEmailAddressToken;
        DateTime originalUnverifiedExpiration = unverifiedUser.VerifyEmailAddressTokenExpiration;

        var migration = GetService<RepairVerifiedUserEmailVerificationMigration>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);

        // Act
        await migration.RunAsync(context);

        // Assert
        var unchangedVerifiedUser = await _userRepository.GetByIdAsync(verifiedUser.Id);
        Assert.NotNull(unchangedVerifiedUser);
        Assert.True(unchangedVerifiedUser.IsEmailAddressVerified);
        Assert.Null(unchangedVerifiedUser.VerifyEmailAddressToken);
        Assert.Equal(DateTime.MinValue, unchangedVerifiedUser.VerifyEmailAddressTokenExpiration);
        Assert.Equal(verifiedUpdatedBeforeMigration, unchangedVerifiedUser.UpdatedUtc);

        var unchangedUnverifiedUser = await _userRepository.GetByIdAsync(unverifiedUser.Id);
        Assert.NotNull(unchangedUnverifiedUser);
        Assert.False(unchangedUnverifiedUser.IsEmailAddressVerified);
        Assert.Equal(originalUnverifiedToken, unchangedUnverifiedUser.VerifyEmailAddressToken);
        Assert.Equal(originalUnverifiedExpiration, unchangedUnverifiedUser.VerifyEmailAddressTokenExpiration);
        Assert.Equal(unverifiedUpdatedBeforeMigration, unchangedUnverifiedUser.UpdatedUtc);
    }

    private async Task AddInvalidVerifiedUserAsync(User user, string? staleToken, DateTime staleExpiration)
    {
        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        bool wasPatched = await _userRepository.PatchAsync(
            user.Id,
            new PartialPatch(new
            {
                verify_email_address_token = staleToken,
                verify_email_address_token_expiration = staleExpiration
            }));
        Assert.True(wasPatched);
    }
}
